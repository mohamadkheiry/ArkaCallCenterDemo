using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Audio;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ArkaCallCenter.Tests;

public sealed class KnowledgeAnswerServiceTests
{
    [Fact]
    public async Task Add_persists_static_audio_and_rejects_normalized_duplicate_without_new_tts_call()
    {
        await using var fixture = await Fixture.CreateAsync();

        var first = await fixture.Service.AddAsync(fixture.UserId,
            "كلاس‌هاي عصر چه ساعتی برگزار می‌شوند؟", "ساعت هجده.");
        var duplicate = await fixture.Service.AddAsync(fixture.UserId,
            "کلاس های عصر چه ساعتی برگزار می شوند", "پاسخ دیگر");

        Assert.True(first.Ok, first.Error);
        Assert.False(duplicate.Ok);
        Assert.Contains("قبلاً ثبت شده", duplicate.Error, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Gap.TtsCalls);
        var saved = await fixture.Db.KnowledgeAnswers.AsNoTracking().SingleAsync();
        Assert.Equal(KnowledgeAnswerAudioStatus.Ready, saved.AudioStatus);
        Assert.True(File.Exists(saved.AudioPath));
        Assert.Equal(KbSourceType.QuestionAnswer,
            (await fixture.Db.KnowledgeBases.AsNoTracking().SingleAsync()).SourceType);
    }

    [Fact]
    public async Task Editing_only_the_question_reuses_audio_but_editing_answer_replaces_it()
    {
        await using var fixture = await Fixture.CreateAsync();
        var added = await fixture.Service.AddAsync(fixture.UserId, "ساعت کلاس چیست؟", "ساعت ده.");
        var id = added.Item!.Id;
        var originalPath = (await fixture.Db.KnowledgeAnswers.AsNoTracking().SingleAsync()).AudioPath;

        var questionOnly = await fixture.Service.UpdateAsync(fixture.UserId, id,
            "کلاس چه ساعتی است؟", "ساعت ده.");
        var unchangedPath = (await fixture.Db.KnowledgeAnswers.AsNoTracking().SingleAsync()).AudioPath;
        var answerChanged = await fixture.Service.UpdateAsync(fixture.UserId, id,
            "کلاس چه ساعتی است؟", "ساعت یازده.");
        var finalPath = (await fixture.Db.KnowledgeAnswers.AsNoTracking().SingleAsync()).AudioPath;

        Assert.True(questionOnly.Ok, questionOnly.Error);
        Assert.True(answerChanged.Ok, answerChanged.Error);
        Assert.Equal(originalPath, unchangedPath);
        Assert.NotEqual(originalPath, finalPath);
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(finalPath));
        Assert.Equal(2, fixture.Gap.TtsCalls);
    }

    [Fact]
    public async Task Unchanged_fallback_is_not_billed_or_regenerated_twice()
    {
        await using var fixture = await Fixture.CreateAsync();

        var first = await fixture.Service.SetFallbackAsync(fixture.UserId,
            "پاسخ این سؤال را نمی‌دانم؛ لطفاً با اپراتور تماس بگیرید.");
        var second = await fixture.Service.SetFallbackAsync(fixture.UserId,
            "پاسخ این سؤال را نمی‌دانم؛ لطفاً با اپراتور تماس بگیرید.");

        Assert.True(first.Ok, first.Error);
        Assert.True(second.Ok, second.Error);
        Assert.True(second.AudioReady);
        Assert.Equal(1, fixture.Gap.TtsCalls);
    }

    [Fact]
    public async Task Exact_normalized_question_returns_only_the_stored_answer_audio()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.AddAsync(fixture.UserId,
            "شماره تماس پشتیبانی چیست؟", "شماره پشتیبانی ۰۲۱۹۱۰۰۸۲۸۸ است.");

        var match = await fixture.Service.MatchAsync(fixture.UserId,
            "شماره تماس پشتیبانی چیست");

        Assert.True(match.Found);
        Assert.Equal(1, match.Score);
        Assert.Equal("شماره پشتیبانی ۰۲۱۹۱۰۰۸۲۸۸ است.", match.Answer);
        Assert.True(File.Exists(match.AudioPath));
        Assert.Equal(0, fixture.Gap.MatcherCalls);
    }

    [Fact]
    public async Task Semantic_judge_accepts_a_real_paraphrase_but_rejects_an_unrelated_question()
    {
        await using var fixture = await Fixture.CreateAsync();
        var added = await fixture.Service.AddAsync(fixture.UserId,
            "کلاس‌های عصر چه ساعتی برگزار می‌شوند؟", "کلاس‌های عصر ساعت هجده برگزار می‌شوند.");
        Assert.True(added.Ok, added.Error);

        fixture.Gap.MatcherSelection = added.Item!.Id;
        var paraphrase = await fixture.Service.MatchAsync(fixture.UserId,
            "کلاسای عصر از چه ساعتی شروع میشن؟");
        fixture.Gap.MatcherSelection = null;
        var unrelated = await fixture.Service.MatchAsync(fixture.UserId,
            "قرمه سبزی چطور درست می‌شود؟");

        Assert.True(paraphrase.Found);
        Assert.Equal(added.Item.Id, paraphrase.Id);
        Assert.Equal("کلاس‌های عصر ساعت هجده برگزار می‌شوند.", paraphrase.Answer);
        Assert.False(unrelated.Found);
        Assert.Equal(2, fixture.Gap.MatcherCalls);
    }

    [Fact]
    public async Task Failed_audio_generation_keeps_the_previous_answer_and_file()
    {
        await using var fixture = await Fixture.CreateAsync();
        var added = await fixture.Service.AddAsync(fixture.UserId, "ساعت کلاس چیست؟", "ساعت ده.");
        var original = await fixture.Db.KnowledgeAnswers.AsNoTracking().SingleAsync();
        fixture.Gap.FailTts = true;

        var result = await fixture.Service.UpdateAsync(fixture.UserId, added.Item!.Id,
            "ساعت کلاس چیست؟", "ساعت یازده.");
        fixture.Db.ChangeTracker.Clear();
        var preserved = await fixture.Db.KnowledgeAnswers.AsNoTracking().SingleAsync();

        Assert.False(result.Ok);
        Assert.Equal("ساعت ده.", preserved.Answer);
        Assert.Equal(original.AudioPath, preserved.AudioPath);
        Assert.True(File.Exists(original.AudioPath));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _uploads;
        public ArkaDbContext Db { get; }
        public FakeGapService Gap { get; }
        public KnowledgeAnswerService Service { get; }
        public int UserId { get; }

        private Fixture(string uploads, ArkaDbContext db, FakeGapService gap,
            KnowledgeAnswerService service, int userId)
        {
            _uploads = uploads;
            Db = db;
            Gap = gap;
            Service = service;
            UserId = userId;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var uploads = Path.Combine(Path.GetTempPath(), $"arka-qa-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(uploads);
            var options = new DbContextOptionsBuilder<ArkaDbContext>()
                .UseInMemoryDatabase($"arka-qa-{Guid.NewGuid():N}")
                .Options;
            var db = new ArkaDbContext(options);
            var user = new User { PhoneNumber = $"demo-{Guid.NewGuid():N}", BrandName = "نمونه" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var gap = new FakeGapService();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Storage:UploadsPath"] = uploads }).Build();
            var service = new KnowledgeAnswerService(
                db,
                gap,
                new AllowModeration(),
                new EmptySettings(),
                configuration,
                NullLogger<KnowledgeAnswerService>.Instance);
            return new Fixture(uploads, db, gap, service, user.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (Directory.Exists(_uploads)) Directory.Delete(_uploads, recursive: true);
        }
    }

    private sealed class FakeGapService : IGapAiService
    {
        public int TtsCalls { get; private set; }
        public int MatcherCalls { get; private set; }
        public int? MatcherSelection { get; set; }
        public bool FailTts { get; set; }

        public Task<string> TranscribeAsync(byte[] wav8k, CancellationToken ct = default)
            => Task.FromResult("");

        public Task<string> CleanTranscriptAsync(string transcript, CancellationToken ct = default)
            => Task.FromResult(transcript);

        public Task<byte[]> GenerateSpeechWav8kAsync(
            string text, string? voice = null, CancellationToken ct = default)
        {
            TtsCalls++;
            if (FailTts) throw new HttpRequestException("provider unavailable");
            return Task.FromResult(AudioConvert.WriteWav(
                Enumerable.Repeat((short)700, 800).ToArray(), AudioConvert.TelephonyRate));
        }

        public Task<int?> SelectMatchingQuestionAsync(string cleanedQuestion,
            IReadOnlyList<GapQuestionCandidate> candidates, CancellationToken ct = default)
        {
            MatcherCalls++;
            return Task.FromResult(MatcherSelection);
        }
    }

    private sealed class AllowModeration : IModerationService
    {
        public Task<ModerationResult> CheckAsync(string content, CancellationToken ct = default)
            => Task.FromResult(new ModerationResult(true, null));
    }

    private sealed class EmptySettings : ISettingsService
    {
        public Task<string?> GetAsync(string key, string? fallback = null, CancellationToken ct = default)
            => Task.FromResult(fallback);
        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
            => Task.FromResult(fallback);
        public Task<double> GetDoubleAsync(string key, double fallback, CancellationToken ct = default)
            => Task.FromResult(fallback);
        public Task SetAsync(string key, string? value, bool isSecret = false, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
    }
}
