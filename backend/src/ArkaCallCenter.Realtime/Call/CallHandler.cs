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

        // نوشتن روی AudioSocket باید هماهنگ باشد (صدای AI و موسیقی انتظار همزمان تلاش می‌کنند بنویسند)
        var writeLock = new SemaphoreSlim(1, 1);
        CancellationTokenSource? holdCts = null;

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

        void StopHold() { try { holdCts?.Cancel(); } catch { } holdCts = null; }

        void StartHold()
        {
            if (holdMusic is null || holdMusic.Length == 0) return;
            StopHold();
            holdCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = holdCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    const int frame = 320; // ۲۰ms در SLIN 8kHz
                    while (!token.IsCancellationRequested)
                    {
                        for (var off = 0; off < holdMusic.Length && !token.IsCancellationRequested; off += frame)
                        {
                            var size = Math.Min(frame, holdMusic.Length - off);
                            var chunk = new byte[size];
                            Array.Copy(holdMusic, off, chunk, 0, size);
                            await WriteLockedAsync(chunk);
                            await Task.Delay(20, token); // پخش هم‌زمان (real-time)
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        realtime.OnAudioDelta += async pcm24k =>
        {
            StopHold(); // صدای AI رسید → موسیقی انتظار قطع شود
            var slin8k = AudioResampler.Downsample24kTo8k(pcm24k);
            Record(slin8k);
            await WriteLockedAsync(slin8k);
        };
        realtime.OnUserSpeechStopped += () => { StartHold(); return Task.CompletedTask; };
        realtime.OnResponseDone += () =>
        {
            StopHold();
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

        await realtime.ConnectAsync(instructions, voice, ct);
        await realtime.GreetAsync(welcome, ct);

        var sw = Stopwatch.StartNew();
        var startedAt = DateTime.UtcNow;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (sw.Elapsed.TotalMinutes >= limitMinutes)
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

        sw.Stop();
        StopHold();

        // ذخیره‌ی فایل ضبط‌شده (WAV ۸kHz)
        string? recordingPath = null;
        if (recordingEnabled && recording.Count > 0)
        {
            try
            {
                byte[] wav;
                lock (recLock) wav = AudioConvert.PcmToWav8k(recording.ToArray(), AudioConvert.TelephonyRate);
                recordingPath = Path.Combine(_uploadsPath, $"call_{Guid.NewGuid():N}.wav");
                await File.WriteAllBytesAsync(recordingPath, wav, ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to save call recording"); }
        }

        var transcriptJson = System.Text.Json.JsonSerializer.Serialize(turns,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        await LogCallAsync(sp.Id, startedAt, (int)sw.Elapsed.TotalSeconds, answeredFromKb, transcriptJson, recordingPath);

        if (Interlocked.Read(ref usageTotal) > 0)
        {
            await RecordUsageAsync(sp.User.Id, sp.User.PhoneNumber, model, apiKey!,
                (int)usagePrompt, (int)usageCompletion, (int)usageTotal);
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

    private async Task LogCallAsync(int smartPhoneId, DateTime startedAt, int durationSeconds, bool answeredFromKb, string transcript, string? recordingPath)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
            db.CallSessions.Add(new CallSession
            {
                SmartPhoneId = smartPhoneId,
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
