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

namespace ArkaCallCenter.Realtime.Call;

/// <summary>مدیریت یک تماس منفرد از طریق AudioSocket و پل آن به OpenAI Realtime.</summary>
public class CallHandler
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CallHandler> _logger;
    private readonly string _uploadsPath;

    /// <summary>یک نوبت گفتگو در رونوشت.</summary>
    private record TranscriptTurn(string Role, string Text);

    public CallHandler(IServiceScopeFactory scopes, IConfiguration config, ILogger<CallHandler> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
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

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        var sp = await db.SmartPhones
            .Include(s => s.User).ThenInclude(u => u.KnowledgeBase)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Extension == extension && s.Status == SmartPhoneStatus.Active, ct);
        if (sp is null)
        {
            _logger.LogWarning("No active smart phone for extension {Ext}.", extension);
            return;
        }

        var kbText = sp.User.KnowledgeBase?.RawText ?? "";
        const string defaultFallback = "پاسخ این سوال در پایگاه دانش من موجود نیست.";
        var fallback = await settings.GetAsync(SettingKeys.FallbackMessageText, defaultFallback, ct) ?? defaultFallback;
        var welcome = sp.WelcomeMessageText ?? "سلام، بفرمایید.";
        var voice = sp.User.VoiceName ?? await settings.GetAsync(SettingKeys.DefaultVoiceName, "alloy", ct) ?? "alloy";
        // سوپر ادمین نامحدود است (سقف دقیقه اعمال نمی‌شود)؛ دقایق مصرف‌شده همچنان برای نمایش ثبت می‌شود.
        var unlimited = sp.User.Role == UserRole.SuperAdmin;
        var limitMinutes = sp.User.CallMinuteLimit
            ?? await settings.GetIntAsync(SettingKeys.DefaultCallMinuteLimit, 30, ct);

        // موسیقی انتظار (حین فکر کردن AI) — SLIN 8kHz خام از تنظیمات
        byte[]? holdMusic = null;
        if ((await settings.GetAsync(SettingKeys.HoldMusicEnabled, "false", ct)) == "true")
        {
            var holdPath = await settings.GetAsync(SettingKeys.HoldMusicPath, null, ct);
            if (!string.IsNullOrEmpty(holdPath) && File.Exists(holdPath))
                holdMusic = await File.ReadAllBytesAsync(holdPath, ct);
        }

        var apiKey = await settings.GetAsync(SettingKeys.OpenAiApiKey, null, ct);
        var baseUrl = await settings.GetAsync(SettingKeys.OpenAiBaseUrl, "https://api.openai.com/v1", ct) ?? "https://api.openai.com/v1";
        var model = await settings.GetAsync(SettingKeys.OpenAiRealtimeModel, "gpt-realtime", ct) ?? "gpt-realtime";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("OpenAI API key not configured; cannot handle realtime call.");
            return;
        }

        var recordingEnabled = (await settings.GetAsync(SettingKeys.CallRecordingEnabled, "true", ct)) != "false";

        var instructions = BuildInstructions(sp.User.BrandName, kbText, fallback);
        var turns = new List<TranscriptTurn>();
        var asstBuf = new StringBuilder();
        var recording = new List<byte>();
        var recLock = new object();
        var answeredFromKb = true;
        long usagePrompt = 0, usageCompletion = 0, usageTotal = 0;

        void Record(byte[] slin) { if (recordingEnabled) lock (recLock) recording.AddRange(slin); }

        await using var realtime = new OpenAiRealtimeClient(apiKey!, baseUrl, model, _logger);

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
                    await WriteLockedAsync(NextOutFrame());
            }
            catch (OperationCanceledException) { }
        }

        realtime.OnAudioDelta += pcm24k =>
        {
            Volatile.Write(ref thinking, 0); // صدای AI رسید → دیگر «فکر کردن» نیست
            var slin8k = AudioResampler.Downsample24kTo8k(pcm24k);
            Record(slin8k);
            EnqueueOut(slin8k); // به‌جای نوشتن مستقیم، وارد صف می‌شود؛ پمپ آن را با آهنگ درست می‌فرستد
            return Task.CompletedTask;
        };
        realtime.OnUserSpeechStarted += () =>
        {
            ClearOut();                      // barge-in: صدای در حال پخشِ AI را فوراً قطع کن
            Volatile.Write(ref thinking, 0);
            _logger.LogInformation("Barge-in: user started speaking on ext {Ext}; cleared AI audio buffer.", extension);
            return Task.CompletedTask;
        };
        realtime.OnUserSpeechStopped += () => { Volatile.Write(ref thinking, 1); return Task.CompletedTask; };
        realtime.OnResponseDone += () =>
        {
            Volatile.Write(ref thinking, 0);
            if (asstBuf.Length > 0)
            {
                var text = asstBuf.ToString().Trim();
                turns.Add(new TranscriptTurn("assistant", text));
                if (!string.IsNullOrEmpty(fallback) && text.Contains(fallback[..Math.Min(15, fallback.Length)]))
                    answeredFromKb = false;
                asstBuf.Clear();
            }
            return Task.CompletedTask;
        };
        realtime.OnAssistantText += text => { asstBuf.Append(text); return Task.CompletedTask; };
        realtime.OnUserTranscript += text =>
        {
            if (!string.IsNullOrWhiteSpace(text)) turns.Add(new TranscriptTurn("user", text.Trim()));
            return Task.CompletedTask;
        };

        // پمپ را از همان ابتدا (پیش از اتصال به OpenAI) شروع کن تا سکوت بلافاصله به
        // Asterisk جاری شود و در طول هندشیک WebSocket هم اتصال زنده بماند.
        var pumpTask = PumpAsync();
        var sw = Stopwatch.StartNew();
        var startedAt = DateTime.UtcNow;
        try
        {
            await realtime.ConnectAsync(instructions, voice, ct);
            await realtime.GreetAsync(welcome, ct);

            while (!ct.IsCancellationRequested)
            {
                if (!unlimited && sw.Elapsed.TotalMinutes >= limitMinutes)
                {
                    _logger.LogInformation("Call on ext {Ext} reached limit ({Min} min).", extension, limitMinutes);
                    break;
                }
                var frame = await AudioSocketProtocol.ReadFrameAsync(stream, ct);
                if (frame is null || frame.Value.Kind == AudioSocketProtocol.KindHangup) break;
                if (frame.Value.Kind == AudioSocketProtocol.KindAudio)
                {
                    Record(frame.Value.Payload); // ضبط صدای caller
                    var pcm24k = AudioResampler.Upsample8kTo24k(frame.Value.Payload);
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
            try { await pumpTask; } catch { }
        }

        // ذخیره‌ی فایل ضبط‌شده (WAV ۸kHz)
        string? recordingPath = null;
        if (recordingEnabled && recording.Count > 0)
        {
            try
            {
                byte[] pcm;
                lock (recLock) pcm = recording.ToArray();
                // کوتاه‌کردنِ سکوت‌های طولانی + حذفِ نویزِ سکوت برای صدای صاف‌تر و فشرده‌تر.
                pcm = AudioPostProcess.CompressSilence(pcm, AudioConvert.TelephonyRate);
                var wav = AudioConvert.PcmToWav8k(pcm, AudioConvert.TelephonyRate);
                recordingPath = Path.Combine(_uploadsPath, $"call_{Guid.NewGuid():N}.wav");
                await File.WriteAllBytesAsync(recordingPath, wav, ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to save call recording"); }
        }

        var transcriptJson = System.Text.Json.JsonSerializer.Serialize(turns,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var durationSeconds = (int)sw.Elapsed.TotalSeconds;
        await LogCallAsync(sp.Id, callerId, startedAt, durationSeconds, answeredFromKb, transcriptJson, recordingPath);

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

    private static string BuildInstructions(string? brand, string kbText, string fallback) => $"""
        تو دستیار صوتی هوشمند برند «{brand}» هستی و به فارسی، مؤدب و کوتاه پاسخ می‌دهی.
        فقط و فقط بر اساس «پایگاه دانش» زیر پاسخ بده. اگر پاسخ سوالِ تماس‌گیرنده در پایگاه
        دانش وجود نداشت، دقیقاً و بدون تغییر این جمله را بگو: «{fallback}» و چیز دیگری اضافه نکن.

        === پایگاه دانش ===
        {kbText}
        === پایان پایگاه دانش ===
        """;

    private async Task LogCallAsync(int smartPhoneId, string? callerId, DateTime startedAt, int durationSeconds, bool answeredFromKb, string transcript, string? recordingPath)
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
