using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Audio;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Realtime.Audio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArkaCallCenter.Realtime.Call;

/// <summary>
/// پاسخ‌گوی تماس بدون OpenAI Realtime. گفتار با VAD محلی جدا می‌شود، Whisper آن را
/// به متن تبدیل می‌کند، GapGPT فقط رونوشت را بازسازی می‌کند و پاسخ صرفاً از صوت ثابت
/// سؤال‌وجواب ذخیره‌شده پخش می‌شود.
/// </summary>
public sealed class QaCallHandler
{
    private const int FrameMs = 20;
    private const int FrameBytes = 320;
    private readonly IServiceScopeFactory _scopes;
    private readonly WelcomeAudioCache _welcomeCache;
    private readonly ILogger<QaCallHandler> _logger;
    private readonly string _uploadsPath;
    private readonly TimeSpan _idleTimeout;
    private readonly int _noiseGateRms;
    private readonly int _speechStartFrames;
    private readonly int _speechEndFrames;
    private readonly int _minimumSpeechFrames;
    private readonly int _maximumUtteranceFrames;
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _speechCache = new();

    private sealed record TranscriptTurn(
        string Role,
        string Text,
        string? RawText = null,
        string? MatchedQuestion = null,
        double? MatchScore = null,
        string? Outcome = null,
        DateTime? At = null);

    public QaCallHandler(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        IOptions<RealtimeOptions> options,
        WelcomeAudioCache welcomeCache,
        ILogger<QaCallHandler> logger)
    {
        _scopes = scopes;
        _welcomeCache = welcomeCache;
        _logger = logger;
        _uploadsPath = configuration["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var value = options.Value;
        _idleTimeout = value.IdleTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(value.IdleTimeoutSeconds)
            : Timeout.InfiniteTimeSpan;
        _noiseGateRms = Math.Clamp(value.InputNoiseGateRms, 20, 2_000);
        _speechStartFrames = Math.Clamp(value.SpeechStartFrames, 1, 10);
        _speechEndFrames = Math.Clamp(value.SpeechEndSilenceMs / FrameMs, 10, 150);
        _minimumSpeechFrames = Math.Clamp(value.MinimumSpeechMs / FrameMs, 3, 100);
        _maximumUtteranceFrames = Math.Clamp(value.MaximumUtteranceSeconds * 1_000 / FrameMs, 100, 3_000);
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task HandleAsync(TcpClient client, CancellationToken stoppingToken)
    {
        using var tcp = client;
        await using var stream = client.GetStream();
        var idFrame = await AudioSocketProtocol.ReadFrameAsync(stream, stoppingToken);
        if (idFrame is null || idFrame.Value.Kind != AudioSocketProtocol.KindId) return;
        var extension = AudioSocketProtocol.ParseExtension(idFrame.Value.Payload);
        if (extension is null) return;
        var callerId = AudioSocketProtocol.ParseCaller(idFrame.Value.Payload);
        var startedAt = DateTime.UtcNow;
        var startedTicks = Stopwatch.GetTimestamp();

        SmartPhone sp;
        Dictionary<string, string?> settings;
        using (var initialScope = _scopes.CreateScope())
        {
            var db = initialScope.ServiceProvider.GetRequiredService<ArkaDbContext>();
            sp = await db.SmartPhones.AsNoTracking().Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Extension == extension &&
                    item.Status == SmartPhoneStatus.Active, stoppingToken)
                ?? throw new InvalidOperationException($"No active smart phone for extension {extension}.");
            if (!sp.User.IsActive) return;
            var keys = new[]
            {
                SettingKeys.FallbackMessageText,
                SettingKeys.DefaultCallMinuteLimit,
                SettingKeys.HoldMusicEnabled,
                SettingKeys.HoldMusicPath,
                SettingKeys.CallRecordingEnabled,
            };
            settings = await db.AppSettings.AsNoTracking().Where(item => keys.Contains(item.Key))
                .ToDictionaryAsync(item => item.Key, item => item.Value, stoppingToken);
        }

        string? GetSetting(string key, string? fallback = null)
            => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        var unlimited = sp.User.Role == UserRole.SuperAdmin;
        var limit = sp.User.CallMinuteLimit ??
            (int.TryParse(GetSetting(SettingKeys.DefaultCallMinuteLimit), out var parsedLimit) ? parsedLimit : 30);
        if (!unlimited && sp.User.UsedMinutes >= limit) return;
        var recordingEnabled = GetSetting(SettingKeys.CallRecordingEnabled, "true") != "false";
        var globalFallback = ConversationMessages.EnsureOperatorEscalation(
            GetSetting(SettingKeys.FallbackMessageText, ConversationMessages.UnknownKnowledge)
            ?? ConversationMessages.UnknownKnowledge);
        byte[]? holdMusic = null;
        var holdPath = GetSetting(SettingKeys.HoldMusicPath);
        if (GetSetting(SettingKeys.HoldMusicEnabled, "false") == "true" &&
            !string.IsNullOrWhiteSpace(holdPath) && File.Exists(holdPath))
            holdMusic = await File.ReadAllBytesAsync(holdPath, stoppingToken);

        var recorder = new CallRecordingBuffer();
        var hasWelcome = _welcomeCache.TryGet(extension.Value, sp.WelcomeAudioPath, out var welcome);
        if (hasWelcome)
            await PlayWelcomeWithoutVadAsync(stream, welcome, extension.Value, startedTicks, recorder, stoppingToken);

        using var callCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var writeLock = new SemaphoreSlim(1, 1);
        var output = new LinkedList<byte[]>();
        var outputLock = new object();
        var outputHead = 0;
        // شناسه نسل به‌جای boolean ساده استفاده می‌شود تا پایان task لغوشدهٔ قبلی،
        // وضعیت «در حال پردازش» نوبت جدید را به اشتباه صفر نکند.
        long thinkingGeneration = 0;
        long nextTurnGeneration = 0;
        var holdPosition = 0;
        var lastActivityTicks = Stopwatch.GetTimestamp();
        var turns = new List<TranscriptTurn>();
        var unanswered = new List<string>();
        var logLock = new object();
        var answeredFromKb = false;
        var pendingTasks = new List<Task>();
        var pendingLock = new object();
        CancellationTokenSource? currentTurn = null;

        void EnqueueOutput(byte[] slin)
        {
            if (slin.Length == 0) return;
            lock (outputLock) output.AddLast(slin);
            Interlocked.Exchange(ref lastActivityTicks, Stopwatch.GetTimestamp());
        }

        void ClearOutput()
        {
            lock (outputLock)
            {
                output.Clear();
                outputHead = 0;
            }
        }

        bool HasOutput()
        {
            lock (outputLock) return output.First is not null;
        }

        byte[] NextOutputFrame()
        {
            var frame = new byte[FrameBytes];
            var filled = 0;
            lock (outputLock)
            {
                while (filled < FrameBytes && output.First is not null)
                {
                    var chunk = output.First.Value;
                    var available = chunk.Length - outputHead;
                    var take = Math.Min(available, FrameBytes - filled);
                    Array.Copy(chunk, outputHead, frame, filled, take);
                    filled += take;
                    outputHead += take;
                    if (outputHead >= chunk.Length)
                    {
                        output.RemoveFirst();
                        outputHead = 0;
                    }
                }
            }
            if (filled == 0 && holdMusic is { Length: > 0 } &&
                Interlocked.Read(ref thinkingGeneration) != 0)
            {
                for (var index = 0; index < frame.Length; index++)
                {
                    frame[index] = holdMusic[holdPosition];
                    holdPosition = (holdPosition + 1) % holdMusic.Length;
                }
            }
            return frame;
        }

        async Task PumpAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(FrameMs));
                while (await timer.WaitForNextTickAsync(callCts.Token))
                {
                    var frame = NextOutputFrame();
                    await writeLock.WaitAsync(callCts.Token);
                    try { await AudioSocketProtocol.WriteAudioAsync(stream, frame, callCts.Token); }
                    finally { writeLock.Release(); }
                    recorder.CapturePlayedFrame(frame);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                _logger.LogDebug(ex, "Audio output ended for ext {Extension}.", extension);
            }
        }

        async Task PlayTextAsync(string text, CancellationToken ct)
        {
            var key = text.Trim();
            var lazy = _speechCache.GetOrAdd(key, _ => new Lazy<Task<byte[]>>(
                async () =>
                {
                    using var scope = _scopes.CreateScope();
                    var gap = scope.ServiceProvider.GetRequiredService<IGapAiService>();
                    var wav = await gap.GenerateSpeechWav8kAsync(key, ct: CancellationToken.None);
                    return AudioConvert.WavToSlin8k(wav);
                }, LazyThreadSafetyMode.ExecutionAndPublication));
            try { EnqueueOutput(await lazy.Value.WaitAsync(ct)); }
            catch
            {
                _speechCache.TryRemove(key, out _);
                throw;
            }
        }

        async Task ProcessUtteranceAsync(byte[] pcm, long generation, CancellationToken turnCt)
        {
            Interlocked.Exchange(ref thinkingGeneration, generation);
            try
            {
                using var scope = _scopes.CreateScope();
                var gap = scope.ServiceProvider.GetRequiredService<IGapAiService>();
                var answers = scope.ServiceProvider.GetRequiredService<IKnowledgeAnswerService>();
                var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
                var wav = AudioConvert.PcmToWav8k(pcm, AudioConvert.TelephonyRate);
                var raw = await gap.TranscribeAsync(wav, turnCt);
                if (!ConversationTurnClassifier.HasMeaningfulInput(raw)) return;
                var cleaned = await gap.CleanTranscriptAsync(raw, turnCt);
                if (!ConversationTurnClassifier.HasMeaningfulInput(cleaned)) return;
                lock (logLock)
                    turns.Add(new TranscriptTurn("user", cleaned, raw, At: DateTime.UtcNow));

                string responseText;
                string outcome;
                string? matchedQuestion = null;
                double? matchScore = null;
                byte[] responseAudio;
                if (ConversationTurnClassifier.TryCreateBusinessIdentityResponse(
                        cleaned, sp.User.BrandName, out responseText) ||
                    ConversationTurnClassifier.TryCreateResponse(cleaned, out responseText))
                {
                    outcome = "conversation";
                    await PlayTextAsync(responseText, turnCt);
                    lock (logLock)
                        turns.Add(new TranscriptTurn("assistant", responseText, Outcome: outcome, At: DateTime.UtcNow));
                    return;
                }

                var match = await answers.MatchAsync(sp.User.Id, cleaned, turnCt);
                if (match.Found && match.AudioPath is not null && File.Exists(match.AudioPath))
                {
                    responseText = match.Answer!;
                    outcome = "matched";
                    matchedQuestion = match.Question;
                    matchScore = match.Score;
                    responseAudio = AudioConvert.WavToSlin8k(await File.ReadAllBytesAsync(match.AudioPath, turnCt));
                    answeredFromKb = true;
                    EnqueueOutput(responseAudio);
                }
                else
                {
                    var fallback = await db.KnowledgeBases.AsNoTracking()
                        .Where(item => item.UserId == sp.User.Id)
                        .Select(item => new { item.FallbackText, item.FallbackAudioPath })
                        .FirstOrDefaultAsync(turnCt);
                    responseText = string.IsNullOrWhiteSpace(fallback?.FallbackText)
                        ? globalFallback
                        : fallback.FallbackText!;
                    outcome = "unanswered";
                    if (!string.IsNullOrWhiteSpace(fallback?.FallbackAudioPath) && File.Exists(fallback.FallbackAudioPath))
                    {
                        responseAudio = AudioConvert.WavToSlin8k(
                            await File.ReadAllBytesAsync(fallback.FallbackAudioPath, turnCt));
                        EnqueueOutput(responseAudio);
                    }
                    else await PlayTextAsync(responseText, turnCt);
                    lock (logLock) unanswered.Add(cleaned);
                }
                lock (logLock)
                    turns.Add(new TranscriptTurn("assistant", responseText, MatchedQuestion: matchedQuestion,
                        MatchScore: matchScore, Outcome: outcome, At: DateTime.UtcNow));
            }
            catch (OperationCanceledException) when (turnCt.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Whisper/clean/match pipeline failed for ext {Extension}; question is not marked unanswered.", extension);
                try
                {
                    const string temporary = "در حال حاضر پردازش سؤال ممکن نیست؛ لطفاً چند لحظه دیگر دوباره تلاش کنید.";
                    await PlayTextAsync(temporary, turnCt);
                    lock (logLock)
                        turns.Add(new TranscriptTurn("assistant", temporary, Outcome: "service_error", At: DateTime.UtcNow));
                }
                catch { }
            }
            finally
            {
                Interlocked.CompareExchange(ref thinkingGeneration, 0, generation);
                Interlocked.Exchange(ref lastActivityTicks, Stopwatch.GetTimestamp());
            }
        }

        void QueueUtterance(byte[] pcm)
        {
            CancellationTokenSource turnCts;
            lock (pendingLock)
            {
                currentTurn?.Cancel();
                currentTurn?.Dispose();
                currentTurn = CancellationTokenSource.CreateLinkedTokenSource(callCts.Token);
                turnCts = currentTurn;
            }
            var generation = Interlocked.Increment(ref nextTurnGeneration);
            var task = ProcessUtteranceAsync(pcm, generation, turnCts.Token);
            lock (pendingLock) pendingTasks.Add(task);
        }

        var pumpTask = PumpAsync();
        var stopwatch = Stopwatch.StartNew();
        var preRoll = new Queue<byte[]>();
        const int preRollFrames = 12;
        MemoryStream? utterance = null;
        var speech = false;
        var speechStartStreak = 0;
        var voicedFrames = 0;
        var silentFrames = 0;
        var utteranceFrames = 0;
        long totalFrames = 0;
        long gatedFrames = 0;

        void FinishUtterance()
        {
            if (utterance is null) return;
            var pcm = utterance.ToArray();
            utterance.Dispose();
            utterance = null;
            if (voicedFrames >= _minimumSpeechFrames) QueueUtterance(pcm);
            speech = false;
            speechStartStreak = 0;
            voicedFrames = 0;
            silentFrames = 0;
            utteranceFrames = 0;
            preRoll.Clear();
        }

        try
        {
            while (!callCts.IsCancellationRequested)
            {
                if (!unlimited && sp.User.UsedMinutes + stopwatch.Elapsed.TotalMinutes >= limit) break;
                if (_idleTimeout != Timeout.InfiniteTimeSpan && !speech &&
                    Interlocked.Read(ref thinkingGeneration) == 0 &&
                    !HasOutput() && Stopwatch.GetElapsedTime(Interlocked.Read(ref lastActivityTicks)) >= _idleTimeout)
                    break;
                var frame = await AudioSocketProtocol.ReadFrameAsync(stream, callCts.Token);
                if (frame is null || frame.Value.Kind == AudioSocketProtocol.KindHangup) break;
                if (frame.Value.Kind != AudioSocketProtocol.KindAudio) continue;
                recorder.EnqueueInbound(frame.Value.Payload);
                totalFrames++;
                var silent = AudioPostProcess.IsSilentFrame(frame.Value.Payload, _noiseGateRms);
                if (silent) gatedFrames++;

                if (!speech)
                {
                    preRoll.Enqueue(frame.Value.Payload.ToArray());
                    while (preRoll.Count > preRollFrames) preRoll.Dequeue();
                    speechStartStreak = silent ? 0 : speechStartStreak + 1;
                    if (speechStartStreak < _speechStartFrames) continue;
                    speech = true;
                    voicedFrames = speechStartStreak;
                    utteranceFrames = preRoll.Count;
                    utterance = new MemoryStream(_maximumUtteranceFrames * FrameBytes);
                    foreach (var buffered in preRoll) utterance.Write(buffered);
                    preRoll.Clear();
                    lock (pendingLock) currentTurn?.Cancel();
                    ClearOutput();
                    Interlocked.Exchange(ref thinkingGeneration, 0);
                    Interlocked.Exchange(ref lastActivityTicks, Stopwatch.GetTimestamp());
                    continue;
                }

                utterance!.Write(frame.Value.Payload);
                utteranceFrames++;
                if (silent) silentFrames++;
                else
                {
                    voicedFrames++;
                    silentFrames = 0;
                    Interlocked.Exchange(ref lastActivityTicks, Stopwatch.GetTimestamp());
                }
                if (silentFrames >= _speechEndFrames || utteranceFrames >= _maximumUtteranceFrames)
                    FinishUtterance();
            }
            if (speech) FinishUtterance();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            _logger.LogDebug(ex, "Caller disconnected from ext {Extension}.", extension);
        }
        finally
        {
            stopwatch.Stop();
            callCts.Cancel();
            lock (pendingLock)
            {
                currentTurn?.Cancel();
                currentTurn?.Dispose();
                currentTurn = null;
            }
            try { await pumpTask; } catch { }
            Task[] tasks;
            lock (pendingLock) tasks = pendingTasks.ToArray();
            try { await Task.WhenAll(tasks); } catch { }
        }

        string transcriptJson;
        string? unansweredJson;
        lock (logLock)
        {
            transcriptJson = JsonSerializer.Serialize(turns,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            unansweredJson = unanswered.Count == 0 ? null : JsonSerializer.Serialize(unanswered);
        }
        string? recordingPath = null;
        if (recordingEnabled)
        {
            try
            {
                var pcm = AudioPostProcess.CompressSilence(recorder.ToArray(), AudioConvert.TelephonyRate);
                if (pcm.Length > 0)
                {
                    recordingPath = Path.Combine(_uploadsPath, $"call_{Guid.NewGuid():N}.wav");
                    await File.WriteAllBytesAsync(recordingPath,
                        AudioConvert.PcmToWav8k(pcm, AudioConvert.TelephonyRate), CancellationToken.None);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not save call recording."); }
        }
        var durationSeconds = (int)stopwatch.Elapsed.TotalSeconds;
        await LogCallAsync(sp.Id, callerId, startedAt, durationSeconds, answeredFromKb,
            transcriptJson, unansweredJson, recordingPath);
        if (durationSeconds > 0)
            await AddUsedMinutesAsync(sp.User.Id, (int)Math.Ceiling(durationSeconds / 60d));
        _logger.LogInformation(
            "QA call ended on ext {Extension}: {Duration}s, gated {Gated}/{Total} frames, {Unanswered} unanswered.",
            extension, durationSeconds, gatedFrames, totalFrames, unanswered.Count);
    }

    private async Task PlayWelcomeWithoutVadAsync(NetworkStream stream, byte[] slin8k, int extension,
        long startedTicks, CallRecordingBuffer recorder, CancellationToken ct)
    {
        using var discardCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var discard = Task.Run(async () =>
        {
            try
            {
                while (!discardCts.IsCancellationRequested)
                {
                    var frame = await AudioSocketProtocol.ReadFrameAsync(stream, discardCts.Token);
                    if (frame is null || frame.Value.Kind == AudioSocketProtocol.KindHangup) return;
                }
            }
            catch (OperationCanceledException) { }
        }, discardCts.Token);
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(FrameMs));
            var logged = false;
            for (var offset = 0; offset < slin8k.Length; offset += FrameBytes)
            {
                if (!await timer.WaitForNextTickAsync(ct)) break;
                var frame = new byte[FrameBytes];
                Array.Copy(slin8k, offset, frame, 0, Math.Min(FrameBytes, slin8k.Length - offset));
                await AudioSocketProtocol.WriteAudioAsync(stream, frame, ct);
                recorder.CapturePlayedFrame(frame);
                if (!logged)
                {
                    logged = true;
                    _logger.LogInformation("First welcome audio for ext {Extension} after {Elapsed:F0}ms.",
                        extension, Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds);
                }
            }
        }
        finally
        {
            discardCts.Cancel();
            try { await discard; } catch { }
        }
    }

    private async Task LogCallAsync(int smartPhoneId, string? callerId, DateTime startedAt,
        int durationSeconds, bool answered, string transcript, string? unanswered, string? recording)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
            db.CallSessions.Add(new CallSession
            {
                SmartPhoneId = smartPhoneId,
                CallerId = callerId,
                StartedAt = startedAt,
                EndedAt = DateTime.UtcNow,
                DurationSeconds = durationSeconds,
                AnsweredFromKb = answered,
                TranscriptJson = transcript,
                UnansweredQuestionsJson = unanswered,
                RecordingPath = recording,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Could not store QA call log."); }
    }

    private async Task AddUsedMinutesAsync(int userId, int minutes)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId);
            if (user is null) return;
            user.UsedMinutes += minutes;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not update used minutes for user {UserId}.", userId); }
    }
}
