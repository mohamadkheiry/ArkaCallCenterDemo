using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Audio;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Realtime;
using ArkaCallCenter.Realtime.Audio;
using ArkaCallCenter.Realtime.Call;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArkaCallCenter.Tests;

public sealed class QaCallHandlerTests
{
    [Fact]
    public async Task Similar_question_plays_the_stored_answer_and_does_not_log_unanswered()
    {
        await using var fixture = await CallFixture.CreateAsync();
        fixture.Answers.Match = new KnowledgeAnswerMatch(true, 7,
            "کلاس‌ها چه ساعتی برگزار می‌شوند؟", "کلاس‌ها ساعت ده برگزار می‌شوند.",
            fixture.AnswerAudioPath, 0.73);

        await fixture.StartAsync();
        await fixture.SendUtteranceAsync();
        Assert.NotEmpty(await fixture.ReadAudibleFrameAsync());
        await fixture.HangupAsync();

        var call = await fixture.ReadCallAsync();
        Assert.True(call.AnsweredFromKb);
        Assert.Null(call.UnansweredQuestionsJson);
        Assert.Contains("\"outcome\":\"matched\"", call.TranscriptJson, StringComparison.Ordinal);
        using var transcript = JsonDocument.Parse(call.TranscriptJson!);
        Assert.Equal("کلاس‌ها چه ساعتی برگزار می‌شوند؟",
            transcript.RootElement.EnumerateArray().Last().GetProperty("matchedQuestion").GetString());
    }

    [Fact]
    public async Task Missing_question_plays_fallback_and_is_logged_once_as_unanswered()
    {
        await using var fixture = await CallFixture.CreateAsync();
        fixture.Answers.Match = new KnowledgeAnswerMatch(false, null, null, null, null, 0.12);

        await fixture.StartAsync();
        await fixture.SendUtteranceAsync();
        Assert.NotEmpty(await fixture.ReadAudibleFrameAsync());
        await fixture.HangupAsync();

        var call = await fixture.ReadCallAsync();
        Assert.False(call.AnsweredFromKb);
        var unanswered = JsonSerializer.Deserialize<string[]>(call.UnansweredQuestionsJson!);
        Assert.Equal(new[] { fixture.Gap.CleanedTranscript }, unanswered);
        Assert.Contains("\"outcome\":\"unanswered\"", call.TranscriptJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Silence_does_not_call_providers_or_create_an_unanswered_question()
    {
        await using var fixture = await CallFixture.CreateAsync();

        await fixture.StartAsync();
        await fixture.SendSilenceAsync(20);
        await fixture.HangupAsync();

        var call = await fixture.ReadCallAsync();
        Assert.Equal(0, fixture.Gap.TranscribeCalls);
        Assert.Equal(0, fixture.Answers.MatchCalls);
        Assert.Null(call.UnansweredQuestionsJson);
        Assert.Equal("[]", call.TranscriptJson);
    }

    [Fact]
    public async Task Provider_error_plays_safe_message_and_is_not_mislabeled_as_unanswered()
    {
        await using var fixture = await CallFixture.CreateAsync();
        fixture.Gap.TranscriptionError = new HttpRequestException("whisper unavailable");

        await fixture.StartAsync();
        await fixture.SendUtteranceAsync();
        Assert.NotEmpty(await fixture.ReadAudibleFrameAsync());
        await fixture.HangupAsync();

        var call = await fixture.ReadCallAsync();
        Assert.Null(call.UnansweredQuestionsJson);
        Assert.Equal(0, fixture.Answers.MatchCalls);
        Assert.Contains("\"outcome\":\"service_error\"", call.TranscriptJson, StringComparison.Ordinal);
    }

    private sealed class CallFixture : IAsyncDisposable
    {
        private const int Extension = 4321;
        private readonly string _root;
        private readonly ServiceProvider _services;
        private readonly QaCallHandler _handler;
        private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(15));
        private TcpClient? _client;
        private Task? _handlerTask;

        public FakeGapService Gap { get; }
        public FakeAnswerService Answers { get; }
        public string AnswerAudioPath { get; }

        private CallFixture(string root, ServiceProvider services, QaCallHandler handler,
            FakeGapService gap, FakeAnswerService answers, string answerAudioPath)
        {
            _root = root;
            _services = services;
            _handler = handler;
            Gap = gap;
            Answers = answers;
            AnswerAudioPath = answerAudioPath;
        }

        public static async Task<CallFixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), $"arka-qa-call-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var answerPath = Path.Combine(root, "answer.wav");
            var fallbackPath = Path.Combine(root, "fallback.wav");
            var wav = AudioConvert.WriteWav(Enumerable.Repeat((short)900, 1_600).ToArray(),
                AudioConvert.TelephonyRate);
            await File.WriteAllBytesAsync(answerPath, wav);
            await File.WriteAllBytesAsync(fallbackPath, wav);

            var gap = new FakeGapService();
            var answers = new FakeAnswerService();
            var services = new ServiceCollection();
            var databaseName = $"arka-qa-call-{Guid.NewGuid():N}";
            services.AddDbContext<ArkaDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.AddSingleton<IGapAiService>(gap);
            services.AddSingleton<IKnowledgeAnswerService>(answers);
            var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
                var user = new User
                {
                    PhoneNumber = $"demo-{Guid.NewGuid():N}",
                    BrandName = "مجموعه آزمایشی",
                    IsActive = true,
                };
                db.Users.Add(user);
                db.SmartPhones.Add(new SmartPhone
                {
                    User = user,
                    Extension = Extension,
                    Status = SmartPhoneStatus.Active,
                });
                db.KnowledgeBases.Add(new KnowledgeBase
                {
                    User = user,
                    SourceType = KbSourceType.QuestionAnswer,
                    ModerationStatus = ModerationStatus.Approved,
                    FallbackText = "پاسخ را نمی‌دانم؛ لطفاً با کارشناس تماس بگیرید.",
                    FallbackAudioPath = fallbackPath,
                });
                db.AppSettings.Add(new AppSetting
                {
                    Key = SettingKeys.CallRecordingEnabled,
                    Value = "false",
                });
                await db.SaveChangesAsync();
            }

            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Storage:UploadsPath"] = root }).Build();
            var options = Options.Create(new RealtimeOptions
            {
                IdleTimeoutSeconds = 5,
                InputNoiseGateRms = 140,
                SpeechStartFrames = 2,
                SpeechEndSilenceMs = 200,
                MinimumSpeechMs = 60,
                MaximumUtteranceSeconds = 3,
            });
            var handler = new QaCallHandler(
                provider.GetRequiredService<IServiceScopeFactory>(),
                config,
                options,
                new WelcomeAudioCache(NullLogger<WelcomeAudioCache>.Instance),
                NullLogger<QaCallHandler>.Instance);
            return new CallFixture(root, provider, handler, gap, answers, answerPath);
        }

        public async Task StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accepted = listener.AcceptTcpClientAsync(_lifetime.Token);
            _client = new TcpClient();
            await _client.ConnectAsync(IPAddress.Loopback, port, _lifetime.Token);
            var server = await accepted;
            listener.Stop();
            _handlerTask = _handler.HandleAsync(server, _lifetime.Token);
            await WriteFrameAsync(AudioSocketProtocol.KindId, ExtensionUuid(), _lifetime.Token);
        }

        public async Task SendUtteranceAsync()
        {
            var voiced = new byte[320];
            for (var offset = 0; offset < voiced.Length; offset += 2)
                BinaryPrimitives.WriteInt16LittleEndian(voiced.AsSpan(offset, 2), 4_000);
            for (var count = 0; count < 6; count++)
                await WriteFrameAsync(AudioSocketProtocol.KindAudio, voiced, _lifetime.Token);
            await SendSilenceAsync(12);
        }

        public async Task SendSilenceAsync(int frames)
        {
            var silence = new byte[320];
            for (var count = 0; count < frames; count++)
                await WriteFrameAsync(AudioSocketProtocol.KindAudio, silence, _lifetime.Token);
        }

        public async Task<byte[]> ReadAudibleFrameAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var stream = _client!.GetStream();
            try
            {
                while (true)
                {
                    var frame = await AudioSocketProtocol.ReadFrameAsync(stream, timeout.Token);
                    if (frame is null) throw new IOException("AudioSocket closed before a response was played.");
                    if (frame.Value.Kind == AudioSocketProtocol.KindAudio &&
                        frame.Value.Payload.Any(value => value != 0))
                        return frame.Value.Payload;
                }
            }
            catch when (_handlerTask?.IsCompleted == true)
            {
                await _handlerTask;
                throw;
            }
        }

        public async Task HangupAsync()
        {
            if (_handlerTask?.IsCompleted == true)
            {
                await _handlerTask;
                return;
            }
            await WriteFrameAsync(AudioSocketProtocol.KindHangup, Array.Empty<byte>(), _lifetime.Token);
            if (_handlerTask is not null) await _handlerTask.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public async Task<CallSession> ReadCallAsync()
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ArkaDbContext>()
                .CallSessions.AsNoTracking().SingleAsync();
        }

        private async Task WriteFrameAsync(byte kind, byte[] payload, CancellationToken ct)
        {
            var frame = new byte[3 + payload.Length];
            frame[0] = kind;
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1, 2), (ushort)payload.Length);
            payload.CopyTo(frame, 3);
            await _client!.GetStream().WriteAsync(frame, ct);
        }

        private static byte[] ExtensionUuid()
        {
            var payload = new byte[16];
            var digits = Extension.ToString("D12");
            for (var index = 0; index < 6; index++)
                payload[10 + index] = Convert.ToByte(digits.Substring(index * 2, 2), 16);
            return payload;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_handlerTask is { IsCompleted: false })
                    await WriteFrameAsync(AudioSocketProtocol.KindHangup, Array.Empty<byte>(), CancellationToken.None);
                if (_handlerTask is not null) await _handlerTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch { }
            _client?.Dispose();
            _lifetime.Dispose();
            await _services.DisposeAsync();
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }
    }

    private sealed class FakeGapService : IGapAiService
    {
        public string CleanedTranscript { get; set; } = "کلاس‌های آزمایشی چه ساعتی برگزار می‌شوند؟";
        public Exception? TranscriptionError { get; set; }
        public int TranscribeCalls { get; private set; }

        public Task<string> TranscribeAsync(byte[] wav8k, CancellationToken ct = default)
        {
            TranscribeCalls++;
            return TranscriptionError is null
                ? Task.FromResult("کلاس ها چه ساعتی بر گزار می شوند")
                : Task.FromException<string>(TranscriptionError);
        }

        public Task<string> CleanTranscriptAsync(string transcript, CancellationToken ct = default)
            => Task.FromResult(CleanedTranscript);

        public Task<byte[]> GenerateSpeechWav8kAsync(
            string text, string? voice = null, CancellationToken ct = default)
            => Task.FromResult(AudioConvert.WriteWav(
                Enumerable.Repeat((short)800, 1_600).ToArray(), AudioConvert.TelephonyRate));

        public Task<int?> SelectMatchingQuestionAsync(string cleanedQuestion,
            IReadOnlyList<GapQuestionCandidate> candidates, CancellationToken ct = default)
            => Task.FromResult<int?>(null);
    }

    private sealed class FakeAnswerService : IKnowledgeAnswerService
    {
        public KnowledgeAnswerMatch Match { get; set; } =
            new(false, null, null, null, null, 0);
        public int MatchCalls { get; private set; }

        public Task<KnowledgeAnswerMatch> MatchAsync(
            int userId, string question, CancellationToken ct = default)
        {
            MatchCalls++;
            return Task.FromResult(Match);
        }

        public Task<KnowledgeAnswerPage> ListAsync(int userId, int skip = 0, int take = 50,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<KnowledgeAnswerResult> AddAsync(int userId, string question, string answer,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<KnowledgeAnswerResult> UpdateAsync(int userId, int id, string question, string answer,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int userId, int id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<KnowledgeAnswerResult> RegenerateAudioAsync(int userId, int id,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<KnowledgeFallbackResult> GetFallbackAsync(int userId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<KnowledgeFallbackResult> SetFallbackAsync(int userId, string text,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
