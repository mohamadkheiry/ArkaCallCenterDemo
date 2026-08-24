using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Audio;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Realtime.Audio;
using ArkaCallCenter.Realtime.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArkaCallCenter.Realtime.Call;

/// <summary>مدیریت یک تماس منفرد از طریق AudioSocket و پل آن به OpenAI Realtime.</summary>
public class CallHandler
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CallHandler> _logger;
    private readonly WelcomeAudioCache _welcomeCache;
    private readonly string _uploadsPath;
    private readonly TimeSpan _idleTimeout;
    private readonly string _transcriptionModel;
    private readonly string _transcriptionLanguage;
    private readonly string _transcriptionPrompt;
    private readonly double _vadThreshold;
    private readonly int _inputNoiseGateRms;

    /// <summary>یک نوبت گفتگو در رونوشت.</summary>
    private record TranscriptTurn(string Role, string Text);

    public CallHandler(IServiceScopeFactory scopes, IConfiguration config, IOptions<RealtimeOptions> realtimeOptions,
        WelcomeAudioCache welcomeCache, ILogger<CallHandler> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _welcomeCache = welcomeCache;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var idleSeconds = realtimeOptions.Value.IdleTimeoutSeconds;
        _idleTimeout = idleSeconds > 0 ? TimeSpan.FromSeconds(idleSeconds) : Timeout.InfiniteTimeSpan;
        _transcriptionModel = realtimeOptions.Value.TranscriptionModel;
        _transcriptionLanguage = realtimeOptions.Value.TranscriptionLanguage;
        _transcriptionPrompt = realtimeOptions.Value.TranscriptionPrompt;
        _vadThreshold = realtimeOptions.Value.VadThreshold;
        _inputNoiseGateRms = Math.Clamp(realtimeOptions.Value.InputNoiseGateRms, 20, 2000);
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using var tcp = client;
        await using var stream = new NetworkStream(client.GetStream().Socket, ownsSocket: false);

        // اولین فریم باید UUID باشد تا شماره‌ی داخلی را بفهمیم.
        var first = await AudioSocketProtocol.ReadFrameAsync(stream, ct);
        if (first is null || first.Value.Kind != AudioSocketProtocol.KindId)
        {
            _logger.LogWarning("AudioSocket connection without ID frame; closing.");
            return;
        }
        var extension = AudioSocketProtocol.ParseExtension(first.Value.Payload);
        if (extension is null)
        {
            _logger.LogWarning("Could not parse extension from UUID.");
            return;
        }
        var callerId = AudioSocketProtocol.ParseCaller(first.Value.Payload);   // شماره‌ی تماس‌گیرنده
        var callStartedTicks = Stopwatch.GetTimestamp();

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
        var knowledge = scope.ServiceProvider.GetRequiredService<IDirectKnowledgeAnswerService>();

        var sp = await db.SmartPhones
            .Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Extension == extension && s.Status == SmartPhoneStatus.Active, ct);
        if (sp is null)
        {
            _logger.LogWarning("No active smart phone for extension {Ext}.", extension);
            return;
        }
        if (!sp.User.IsActive)
        {
            _logger.LogWarning("Ext {Ext}: owner user {UserId} is deactivated; rejecting call.", extension, sp.User.Id);
            return;
        }

        // Chat usage for direct knowledge answering must be attributed to this caller.
        // The knowledge answer service and usage tracker share this request scope.
        var usageContext = scope.ServiceProvider.GetRequiredService<IUsageContext>();
        usageContext.UserId = sp.User.Id;
        usageContext.PhoneNumber = callerId;

        var recorder = new CallRecordingBuffer();
        using var welcomePlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var hasStaticWelcome = _welcomeCache.TryGet(extension.Value, sp.WelcomeAudioPath, out var staticWelcome);

        // فایل ثابت خوش‌آمد پیش از اتصال به OpenAI پخش می‌شود. در این بازه صدای ورودی
        // تماس‌گیرنده خوانده و دور ریخته می‌شود؛ بنابراین نه به VAD می‌رسد و نه می‌تواند
        // پیام خوش‌آمد را قطع یا به‌عنوان نوبت مکالمه ثبت کند.
        var welcomePlaybackTask = hasStaticWelcome
            ? PlayStaticWelcomeWithoutVadAsync(
                stream,
                staticWelcome,
                extension.Value,
                callStartedTicks,
                recorder,
                welcomePlaybackCts.Token)
            : Task.CompletedTask;

        // A single settings query avoids several sequential database round trips before
        // the greeting starts, which is audible as dead air on the first call after restart.
        var runtimeSettingKeys = new[]
        {
            SettingKeys.FallbackMessageText,
            SettingKeys.DefaultVoiceName,
            SettingKeys.DefaultCallMinuteLimit,
            SettingKeys.HoldMusicEnabled,
            SettingKeys.HoldMusicPath,
            SettingKeys.OpenAiApiKey,
            SettingKeys.OpenAiBaseUrl,
            SettingKeys.OpenAiRealtimeModel,
            SettingKeys.CallRecordingEnabled,
        };
        var runtimeSettings = await db.AppSettings
            .AsNoTracking()
            .Where(setting => runtimeSettingKeys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, ct);
        string? GetSetting(string key, string? fallback = null)
            => runtimeSettings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        int GetIntSetting(string key, int fallback)
            => int.TryParse(GetSetting(key), out var value) ? value : fallback;

        const string defaultFallback = ConversationMessages.UnknownKnowledge;
        var configuredFallback = GetSetting(SettingKeys.FallbackMessageText, defaultFallback) ?? defaultFallback;
        var fallback = ConversationMessages.EnsureOperatorEscalation(configuredFallback);
        var voice = sp.User.VoiceName ?? GetSetting(SettingKeys.DefaultVoiceName, "alloy") ?? "alloy";
        // درصدِ دقت/پایبندی به پایگاه دانش (۱۰..۱۰۰). چون Realtime GA دیگر temperature ندارد،
        // این پارامتر از طریقِ instructions (پرامپت) به مدل منتقل می‌شود؛ درصدِ بالاتر = پایبندیِ سخت‌گیرانه‌تر.
        var accuracy = Math.Clamp(sp.AnswerAccuracyPercent <= 0 ? 70 : sp.AnswerAccuracyPercent, 10, 100);
        // سوپر ادمین نامحدود است (سقف دقیقه اعمال نمی‌شود)؛ دقایق مصرف‌شده همچنان برای نمایش ثبت می‌شود.
        var unlimited = sp.User.Role == UserRole.SuperAdmin;
        var limitMinutes = sp.User.CallMinuteLimit
            ?? GetIntSetting(SettingKeys.DefaultCallMinuteLimit, 30);
        var alreadyUsedMinutes = sp.User.UsedMinutes;   // مصرفِ انباشته پیش از این تماس (snapshot).
        // اگر سقفِ دقیقه قبلاً پر شده، اصلاً تماس را برقرار نکن (اتلافِ توکنِ OpenAI بی‌مورد).
        if (!unlimited && alreadyUsedMinutes >= limitMinutes)
        {
            _logger.LogInformation("Ext {Ext}: minute limit already reached ({Used}/{Limit}); rejecting call.",
                extension, alreadyUsedMinutes, limitMinutes);
            welcomePlaybackCts.Cancel();
            try { await welcomePlaybackTask; } catch (OperationCanceledException) { }
            return;
        }

        // موسیقی انتظار (حین فکر کردن AI) — SLIN 8kHz خام از تنظیمات
        byte[]? holdMusic = null;
        if (GetSetting(SettingKeys.HoldMusicEnabled, "false") == "true")
        {
            var holdPath = GetSetting(SettingKeys.HoldMusicPath);
            if (!string.IsNullOrEmpty(holdPath) && File.Exists(holdPath))
                holdMusic = await File.ReadAllBytesAsync(holdPath, ct);
        }

        var apiKey = GetSetting(SettingKeys.OpenAiApiKey);
        var baseUrl = GetSetting(SettingKeys.OpenAiBaseUrl, "https://api.openai.com/v1") ?? "https://api.openai.com/v1";
        var model = GetSetting(SettingKeys.OpenAiRealtimeModel, "gpt-realtime-2.1") ?? "gpt-realtime-2.1";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("OpenAI API key not configured; cannot handle realtime call.");
            welcomePlaybackCts.Cancel();
            try { await welcomePlaybackTask; } catch (OperationCanceledException) { }
            return;
        }

        var recordingEnabled = GetSetting(SettingKeys.CallRecordingEnabled, "true") != "false";

        var instructions = BuildInstructions(sp.User.BrandName, accuracy);
        var turns = new List<TranscriptTurn>();
        var turnsLock = new object();   // turns از رشته‌ی حلقه‌ی دریافت پر می‌شود و در پایان از رشته‌ی اصلی خوانده می‌شود.
        var conversationMemory = new List<DirectKnowledgeConversationTurn>();
        var conversationMemoryLock = new object(); // حافظهٔ کوتاه و مجزای همین تماس برای سؤال‌های پیرو.
        var asstBuf = new StringBuilder();
        var unanswered = new List<string>();   // سوالاتی که پاسخشان در KB نبود (fallback پخش شد).
        var answeredFromKb = false;
        long inputFrames = 0, noiseGatedFrames = 0;
        long usagePrompt = 0, usageCompletion = 0, usageTotal = 0;
        var userSpeaking = 0;
        var lastSpeechTicks = Stopwatch.GetTimestamp();
        CancellationTokenSource? pendingTurnCts = null;
        var pendingTurnLock = new object();
        var pendingTurns = new List<Task>();
        using var knowledgeGate = new SemaphoreSlim(1, 1);

        await using var realtime = new OpenAiRealtimeClient(apiKey!, baseUrl, model,
            _transcriptionModel, _transcriptionLanguage, _transcriptionPrompt, _vadThreshold, _logger);

        realtime.OnUsage += (p, c, t) =>
        {
            Interlocked.Add(ref usagePrompt, p);
            Interlocked.Add(ref usageCompletion, c);
            Interlocked.Add(ref usageTotal, t);
        };

        // === مسیر خروجی صدا به سمت Asterisk ===
        // AudioSocket نیازمند جریانِ پیوسته‌ی صدا (هر ۲۰ms یک فریم) است؛ اگر worker
        // ساکت بماند، Asterisk با خطای «Failed to read data from AudioSocket» تماس را
        // قطع می‌کند. پس یک «پمپ» داریم که هر ۲۰ms دقیقاً ۳۲۰ بایت می‌فرستد: یا صدای
        // AI از صف، یا موسیقی انتظار حین فکر کردن، یا سکوت. این هم اتصال را زنده نگه
        // می‌دارد و هم پخش را با آهنگِ درست (real-time) هماهنگ می‌کند.
        using var callCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var writeLock = new SemaphoreSlim(1, 1);
        var outChunks = new LinkedList<byte[]>();   // صف صدای AI (SLIN 8kHz)
        var outLock = new object();
        var outHead = 0;                            // آفست خواندن در اولین قطعه‌ی صف
        var thinking = 0;                           // ۱ = کاربر حرفش تمام شده، منتظر پاسخ AI
        var holdPos = 0;                            // موقعیت پخش موسیقی انتظار (لوپ)
        var firstAudioLogged = hasStaticWelcome ? 1 : 0;

        void EnqueueOut(byte[] slin) { lock (outLock) outChunks.AddLast(slin); }

        // خالی‌کردن فوریِ صف پخش — برای barge-in: وقتی کاربر وسط حرف AI شروع به صحبت می‌کند،
        // صدای بافرشده‌ی AI باید بلافاصله قطع شود تا AI ساکت شود و به کاربر گوش دهد.
        void ClearOut() { lock (outLock) { outChunks.Clear(); outHead = 0; } }

        async Task WriteLockedAsync(byte[] slin)
        {
            if (ct.IsCancellationRequested) return;
            await writeLock.WaitAsync(ct);
            try { await AudioSocketProtocol.WriteAudioAsync(stream, slin, ct); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Net.Sockets.SocketException)
            {
                // تماس‌گیرنده قطع کرد / سوکت بسته شد — عادی است، نادیده بگیر.
            }
            finally { writeLock.Release(); }
        }

        // یک فریم ۳۲۰ بایتی (۲۰ms) بساز: اول از صف صدای AI؛ اگر خالی بود و AI در حال
        // فکر کردن است، موسیقی انتظار (لوپ)؛ در غیر این صورت سکوت.
        byte[] NextOutFrame()
        {
            const int frameLen = 320;
            var frame = new byte[frameLen];
            var filled = 0;
            lock (outLock)
            {
                while (filled < frameLen && outChunks.First is not null)
                {
                    var chunk = outChunks.First.Value;
                    var avail = chunk.Length - outHead;
                    var take = Math.Min(avail, frameLen - filled);
                    Array.Copy(chunk, outHead, frame, filled, take);
                    filled += take; outHead += take;
                    if (outHead >= chunk.Length) { outChunks.RemoveFirst(); outHead = 0; }
                }
            }
            if (filled == 0 && holdMusic is { Length: > 0 } && Volatile.Read(ref thinking) == 1)
            {
                for (var i = 0; i < frameLen; i++)
                {
                    frame[i] = holdMusic[holdPos];
                    if (++holdPos >= holdMusic.Length) holdPos = 0;
                }
            }
            if (filled > 0 && Interlocked.CompareExchange(ref firstAudioLogged, 1, 0) == 0)
            {
                _logger.LogInformation("First greeting audio queued for ext {Ext} after {ElapsedMs:F0}ms.",
                    extension, Stopwatch.GetElapsedTime(callStartedTicks).TotalMilliseconds);
            }
            // در غیر این صورت فریمِ سکوت (صفر) می‌ماند
            return frame;
        }

        // پمپِ خروجی: تا پایان تماس، هر ۲۰ms یک فریم به Asterisk می‌فرستد.
        async Task PumpAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
                while (await timer.WaitForNextTickAsync(callCts.Token))
                {
                    var playedFrame = NextOutFrame();
                    await WriteLockedAsync(playedFrame);
                    recorder.CapturePlayedFrame(playedFrame);
                }
            }
            catch (OperationCanceledException) { }
        }

        realtime.OnAudioDelta += pcm24k =>
        {
            Interlocked.Exchange(ref lastSpeechTicks, Stopwatch.GetTimestamp());
            Volatile.Write(ref thinking, 0); // صدای AI رسید → دیگر «فکر کردن» نیست
            var slin8k = AudioResampler.Downsample24kTo8k(pcm24k);
            EnqueueOut(slin8k); // به‌جای نوشتن مستقیم، وارد صف می‌شود؛ پمپ آن را با آهنگ درست می‌فرستد
            return Task.CompletedTask;
        };
        realtime.OnUserSpeechStarted += () =>
        {
            Interlocked.Exchange(ref userSpeaking, 1);
            Interlocked.Exchange(ref lastSpeechTicks, Stopwatch.GetTimestamp());
            lock (pendingTurnLock)
            {
                pendingTurnCts?.Cancel();
                pendingTurnCts?.Dispose();
                pendingTurnCts = null;
            }
            ClearOut();                      // barge-in: صدای در حال پخشِ AI را فوراً قطع کن
            Volatile.Write(ref thinking, 0);
            _logger.LogInformation("Barge-in: user started speaking on ext {Ext}; cleared AI audio buffer.", extension);
            return Task.CompletedTask;
        };
        realtime.OnUserSpeechStopped += () =>
        {
            Interlocked.Exchange(ref userSpeaking, 0);
            Interlocked.Exchange(ref lastSpeechTicks, Stopwatch.GetTimestamp());
            Volatile.Write(ref thinking, 1);
            return Task.CompletedTask;
        };
        realtime.OnResponseDone += () =>
        {
            Volatile.Write(ref thinking, 0);
            if (asstBuf.Length > 0)
            {
                var text = asstBuf.ToString().Trim();
                lock (turnsLock) turns.Add(new TranscriptTurn("assistant", text));
                asstBuf.Clear();
            }
            return Task.CompletedTask;
        };
        realtime.OnAssistantText += text => { asstBuf.Append(text); return Task.CompletedTask; };
        realtime.OnUserTranscript += text =>
        {
            if (!ConversationTurnClassifier.HasMeaningfulInput(text))
            {
                Volatile.Write(ref thinking, 0);
                _logger.LogInformation("Ignoring empty/non-lexical transcript on ext {Ext}.", extension);
                return Task.CompletedTask;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var question = text.Trim();
                lock (turnsLock) turns.Add(new TranscriptTurn("user", question));

                // اگر transcription نوبت قبلی بعد از شروع صحبت تازه رسید، پاسخ قدیمی را نساز؛
                // نوبت تازه پس از توقف گفتار، transcription و پاسخ مستقل خودش را خواهد داشت.
                if (Volatile.Read(ref userSpeaking) != 0)
                {
                    _logger.LogInformation("Ignoring stale transcript while caller is speaking on ext {Ext}.", extension);
                    return Task.CompletedTask;
                }

                CancellationTokenSource turnCts;
                lock (pendingTurnLock)
                {
                    pendingTurnCts?.Cancel();
                    pendingTurnCts?.Dispose();
                    pendingTurnCts = CancellationTokenSource.CreateLinkedTokenSource(callCts.Token);
                    turnCts = pendingTurnCts;
                }

                var task = AnswerFromKnowledgeAsync(question, turnCts.Token);
                lock (pendingTurnLock) pendingTurns.Add(task);
            }
            return Task.CompletedTask;
        };

        async Task AnswerFromKnowledgeAsync(string question, CancellationToken turnCt)
        {
            try
            {
                if (ConversationTurnClassifier.TryCreateBusinessIdentityResponse(
                        question,
                        sp.User.BrandName,
                        out var identityResponse))
                {
                    _logger.LogInformation(
                        "Answering business identity question for ext {Ext}: {Question}",
                        extension,
                        question);
                    await realtime.CreateConversationalResponseAsync(identityResponse, turnCt);
                    RememberConversationTurn(question, identityResponse);
                    return;
                }

                if (ConversationTurnClassifier.TryCreateResponse(question, out var conversationalResponse))
                {
                    _logger.LogInformation(
                        "Handling conversational turn without knowledge AI for ext {Ext}: {Question}",
                        extension,
                        question);
                    await realtime.CreateConversationalResponseAsync(conversationalResponse, turnCt);
                    RememberConversationTurn(question, conversationalResponse);
                    return;
                }

                await knowledgeGate.WaitAsync(turnCt);
                DirectKnowledgeAnswer result;
                IReadOnlyList<DirectKnowledgeConversationTurn> historySnapshot;
                lock (conversationMemoryLock)
                    historySnapshot = conversationMemory.ToArray();
                try
                {
                    result = await knowledge.AnswerAsync(
                        sp.User.Id,
                        question,
                        accuracy,
                        historySnapshot,
                        turnCt);
                }
                finally { knowledgeGate.Release(); }

                switch (result.Outcome)
                {
                    case DirectKnowledgeOutcome.Answered:
                        if (string.IsNullOrWhiteSpace(result.AnswerText))
                            throw new InvalidOperationException("Direct knowledge answer was empty.");
                        answeredFromKb = true;
                        _logger.LogInformation(
                            "Answering from the complete knowledge base for ext {Ext}: {Question}",
                            extension,
                            question);
                        await realtime.CreateConversationalResponseAsync(result.AnswerText, turnCt);
                        RememberConversationTurn(question, result.AnswerText);
                        return;

                    case DirectKnowledgeOutcome.NeedsClarification:
                        _logger.LogInformation(
                            "Asking for clarification on a contextual follow-up for ext {Ext}: {Question}",
                            extension,
                            question);
                        await realtime.CreateConversationalResponseAsync(
                            ConversationMessages.FollowUpClarification,
                            turnCt);
                        RememberConversationTurn(question, ConversationMessages.FollowUpClarification);
                        return;

                    case DirectKnowledgeOutcome.OutOfDomain:
                        var scopeResponse = ConversationMessages.CreateOutOfDomain(
                            sp.User.BrandName,
                            result.ScopeDescription);
                        _logger.LogInformation(
                            "Answering out-of-domain question without knowledge fallback for ext {Ext}: {Question}",
                            extension,
                            question);
                        await realtime.CreateConversationalResponseAsync(scopeResponse, turnCt);
                        RememberConversationTurn(question, scopeResponse);
                        return;

                    case DirectKnowledgeOutcome.InDomainUnknown:
                    case DirectKnowledgeOutcome.KnowledgeBaseEmpty:
                        lock (turnsLock) unanswered.Add(question);
                        _logger.LogInformation(
                            "No grounded answer for in-domain question on ext {Ext}: {Question}",
                            extension,
                            question);
                        await realtime.CreateConversationalResponseAsync(fallback, turnCt);
                        RememberConversationTurn(question, fallback);
                        return;

                    case DirectKnowledgeOutcome.KnowledgeBaseTooLarge:
                        _logger.LogWarning(
                            "Complete knowledge base exceeds the safe direct-context limit for ext {Ext}; not recording as unanswered: {Question}",
                            extension,
                            question);
                        await realtime.CreateConversationalResponseAsync(
                            ConversationMessages.RetrievalUnavailable,
                            turnCt);
                        return;

                    case DirectKnowledgeOutcome.ServiceUnavailable:
                        _logger.LogWarning(
                            "Direct knowledge answering unavailable for ext {Ext}; not recording as unanswered: {Question}",
                            extension,
                            question);
                        await realtime.CreateConversationalResponseAsync(
                            ConversationMessages.RetrievalUnavailable,
                            turnCt);
                        return;

                    default:
                        throw new InvalidOperationException($"Unsupported direct knowledge outcome: {result.Outcome}");
                }
            }
            catch (OperationCanceledException) when (turnCt.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Direct knowledge answering failed for ext {Ext}; reporting a temporary problem without marking the question unanswered.",
                    extension);
                try
                {
                    await realtime.CreateConversationalResponseAsync(
                        ConversationMessages.RetrievalUnavailable,
                        turnCt);
                }
                catch (OperationCanceledException) when (turnCt.IsCancellationRequested) { }
            }
        }

        void RememberConversationTurn(string userText, string assistantText)
        {
            lock (conversationMemoryLock)
            {
                conversationMemory.Add(new DirectKnowledgeConversationTurn("user", userText));
                conversationMemory.Add(new DirectKnowledgeConversationTurn("assistant", assistantText));
                // سرویس دانش نیز تاریخچه را محدود می‌کند؛ این برش زودهنگام مانع رشد حافظهٔ تماس می‌شود.
                if (conversationMemory.Count > 6)
                    conversationMemory.RemoveRange(0, conversationMemory.Count - 6);
            }
        }

        Task pumpTask = Task.CompletedTask;
        var sw = Stopwatch.StartNew();
        var startedAt = DateTime.UtcNow;
        try
        {
            // VAD فقط پس از پایان کامل فایل ثابت خوش‌آمد فعال می‌شود.
            await welcomePlaybackTask;
            if (hasStaticWelcome)
                _logger.LogInformation(
                    "Static welcome playback completed before Realtime/VAD connection for ext {Ext}.",
                    extension);
            pumpTask = PumpAsync();
            if (!hasStaticWelcome)
                _logger.LogWarning("Ext {Ext} has no valid static welcome WAV; continuing without greeting.", extension);
            await realtime.ConnectAsync(instructions, voice, ct);

            while (!ct.IsCancellationRequested)
            {
                // سقف بر مبنای مصرفِ انباشته + مدتِ همین تماس (نه فقط همین تماس).
                if (!unlimited && alreadyUsedMinutes + sw.Elapsed.TotalMinutes >= limitMinutes)
                {
                    _logger.LogInformation("Call on ext {Ext} reached limit ({Used}+{Cur:F1}/{Min} min).",
                        extension, alreadyUsedMinutes, sw.Elapsed.TotalMinutes, limitMinutes);
                    break;
                }
                if (_idleTimeout != Timeout.InfiniteTimeSpan &&
                    Volatile.Read(ref userSpeaking) == 0 &&
                    Volatile.Read(ref thinking) == 0 &&
                    Stopwatch.GetElapsedTime(Interlocked.Read(ref lastSpeechTicks)) >= _idleTimeout)
                {
                    _logger.LogInformation("Call on ext {Ext} closed after {Seconds}s of silence.",
                        extension, (int)_idleTimeout.TotalSeconds);
                    break;
                }
                var frame = await AudioSocketProtocol.ReadFrameAsync(stream, ct);
                if (frame is null || frame.Value.Kind == AudioSocketProtocol.KindHangup) break;
                if (frame.Value.Kind == AudioSocketProtocol.KindAudio)
                {
                    // ضبط روی clock پخش انجام می‌شود تا صدای caller و AI روی یک timeline باشند.
                    recorder.EnqueueInbound(frame.Value.Payload);
                    Interlocked.Increment(ref inputFrames);
                    var isLowLevelNoise = AudioPostProcess.IsSilentFrame(frame.Value.Payload, _inputNoiseGateRms);
                    if (isLowLevelNoise) Interlocked.Increment(ref noiseGatedFrames);
                    var pcm24k = isLowLevelNoise
                        ? new byte[frame.Value.Payload.Length * 3]
                        : AudioResampler.Upsample8kTo24k(frame.Value.Payload);
                    await realtime.AppendAudioAsync(pcm24k, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during call on ext {Ext}", extension);
        }
        finally
        {
            sw.Stop();
            callCts.Cancel();                // پمپ خروجی را متوقف کن
            lock (pendingTurnLock)
            {
                pendingTurnCts?.Cancel();
                pendingTurnCts?.Dispose();
                pendingTurnCts = null;
            }
            try { await pumpTask; } catch { }
            Task[] pendingSnapshot;
            lock (pendingTurnLock) pendingSnapshot = pendingTurns.ToArray();
            try { await Task.WhenAll(pendingSnapshot); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogWarning(ex, "Pending knowledge response failed during call shutdown."); }
        }

        // ذخیره‌ی فایل ضبط‌شده (WAV ۸kHz)
        string? recordingPath = null;
        if (recordingEnabled)
        {
            try
            {
                var pcm = recorder.ToArray();
                if (pcm.Length == 0) throw new InvalidOperationException("Call recording is empty.");
                // فقط وقفه‌های واقعاً طولانی را در سطح فریم کوتاه کن؛ نمونه‌های کم‌صدای کلمات دست‌نخورده می‌مانند.
                pcm = AudioPostProcess.CompressSilence(pcm, AudioConvert.TelephonyRate);
                var wav = AudioConvert.PcmToWav8k(pcm, AudioConvert.TelephonyRate);
                recordingPath = Path.Combine(_uploadsPath, $"call_{Guid.NewGuid():N}.wav");
                await File.WriteAllBytesAsync(recordingPath, wav, ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to save call recording"); }
        }

        List<TranscriptTurn> turnsSnapshot;
        List<string> unansweredSnapshot;
        lock (turnsLock)
        {
            turnsSnapshot = new List<TranscriptTurn>(turns);
            unansweredSnapshot = new List<string>(unanswered);
        }
        var transcriptJson = System.Text.Json.JsonSerializer.Serialize(turnsSnapshot,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var unansweredJson = unansweredSnapshot.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(unansweredSnapshot)
            : null;
        var durationSeconds = (int)sw.Elapsed.TotalSeconds;
        _logger.LogInformation(
            "Input noise gate on ext {Ext}: gated {GatedFrames} of {InputFrames} frames (RMS threshold {Threshold}).",
            extension,
            Interlocked.Read(ref noiseGatedFrames),
            Interlocked.Read(ref inputFrames),
            _inputNoiseGateRms);
        await LogCallAsync(sp.Id, callerId, startedAt, durationSeconds, answeredFromKb, transcriptJson, unansweredJson, recordingPath);

        // افزودن دقایق مصرف‌شده به کاربر (هر تماس به بالاترین دقیقه گرد می‌شود؛ مثل صورتحساب مخابراتی).
        // برای سوپر ادمین که نامحدود است هم فقط جهت نمایش انباشته می‌شود.
        if (durationSeconds > 0)
            await AddUsedMinutesAsync(sp.User.Id, (int)Math.Ceiling(durationSeconds / 60.0));

        if (Interlocked.Read(ref usageTotal) > 0)
        {
            await RecordUsageAsync(sp.User.Id, sp.User.PhoneNumber, model, apiKey!,
                (int)usagePrompt, (int)usageCompletion, (int)usageTotal);
        }
    }

    private async Task PlayStaticWelcomeWithoutVadAsync(
        NetworkStream stream,
        byte[] slin8k,
        int extension,
        long startedTicks,
        CallRecordingBuffer recorder,
        CancellationToken ct)
    {
        using var discardCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var discardTask = DiscardInboundDuringWelcomeAsync(stream, discardCts.Token);
        try
        {
            await PlayCachedWelcomeAsync(stream, slin8k, extension, startedTicks, recorder, ct);
        }
        finally
        {
            discardCts.Cancel();
            try { await discardTask; }
            catch (OperationCanceledException) { }
            catch (IOException) when (discardCts.IsCancellationRequested) { }
            catch (System.Net.Sockets.SocketException) when (discardCts.IsCancellationRequested) { }
        }
    }

    private static async Task DiscardInboundDuringWelcomeAsync(NetworkStream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var frame = await AudioSocketProtocol.ReadFrameAsync(stream, ct);
            if (frame is null || frame.Value.Kind == AudioSocketProtocol.KindHangup)
                return;
            // Audio frames during the fixed welcome are intentionally discarded.
        }
    }

    private async Task PlayCachedWelcomeAsync(NetworkStream stream, byte[] slin8k, int extension,
        long startedTicks, CallRecordingBuffer recorder, CancellationToken ct)
    {
        const int frameSize = CallRecordingBuffer.FrameBytes;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        var logged = false;
        for (var offset = 0; offset < slin8k.Length; offset += frameSize)
        {
            if (!await timer.WaitForNextTickAsync(ct)) break;
            var size = Math.Min(frameSize, slin8k.Length - offset);
            var frame = new byte[frameSize];
            Array.Copy(slin8k, offset, frame, 0, size);
            await AudioSocketProtocol.WriteAudioAsync(stream, frame, ct);
            recorder.CapturePlayedFrame(frame);
            if (!logged)
            {
                logged = true;
                _logger.LogInformation("First greeting audio queued for ext {Ext} after {ElapsedMs:F0}ms (memory cache).",
                    extension, Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds);
            }
        }
    }

    private async Task AddUsedMinutesAsync(int userId, int minutes)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return;
            user.UsedMinutes += minutes;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update used minutes for user {UserId}", userId);
        }
    }

    private async Task RecordUsageAsync(int userId, string phone, string model, string apiKey,
        int prompt, int completion, int total)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IUsageContext>();
            ctx.UserId = userId;
            ctx.PhoneNumber = phone;
            var tracker = scope.ServiceProvider.GetRequiredService<ITokenUsageTracker>();
            await tracker.RecordAsync("Realtime", model, ApiKeyFingerprint.Of(apiKey), prompt, completion, total);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record realtime token usage.");
        }
    }

    private static string BuildInstructions(string? brand, int accuracyPercent)
    {
        var readingStyle = accuracyPercent switch
        {
            >= 80 => "متن تأییدشده را دقیق، شمرده و بدون افزودن توضیح دیگر بخوان.",
            >= 40 => "متن تأییدشده را روان و طبیعی، بدون تغییر معنا بخوان.",
            _ => "متن تأییدشده را کاملاً محاوره‌ای اما بدون افزودن اطلاعات جدید بخوان.",
        };
        return $"""
        تو دستیار صوتی هوشمند برند «{brand}» هستی و به فارسی، مؤدب و کوتاه پاسخ می‌دهی.
        با فارسی معیار ایران، کاملاً روان و طبیعی و بدون لهجه انگلیسی صحبت کن؛ از مکث‌های غیرضروری پرهیز کن.
        سامانه پیش از هر پاسخ، کل پایگاه دانش را جداگانه بررسی و پاسخ نهایی را تأیید می‌کند.
        هرگاه متن تأییدشده در دستور پاسخ ارائه شد، فقط همان متن را بخوان و هیچ اطلاعاتی از حافظه یا دانش عمومی اضافه نکن.
        سبک خواندن: {readingStyle}
        """;
    }

    private async Task LogCallAsync(int smartPhoneId, string? callerId, DateTime startedAt, int durationSeconds, bool answeredFromKb, string transcript, string? unansweredJson, string? recordingPath)
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
                AnsweredFromKb = answeredFromKb,
                TranscriptJson = transcript,
                UnansweredQuestionsJson = unansweredJson,
                RecordingPath = recordingPath,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log call session.");
        }
    }
}
