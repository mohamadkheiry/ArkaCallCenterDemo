using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Realtime.Audio;
using ArkaCallCenter.Realtime.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Realtime.Call;

/// <summary>مدیریت یک تماس منفرد از طریق AudioSocket و پل آن به OpenAI Realtime.</summary>
public class CallHandler
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CallHandler> _logger;

    public CallHandler(IServiceScopeFactory scopes, ILogger<CallHandler> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
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

        var apiKey = await settings.GetAsync(SettingKeys.OpenAiApiKey, null, ct);
        var baseUrl = await settings.GetAsync(SettingKeys.OpenAiBaseUrl, "https://api.openai.com/v1", ct) ?? "https://api.openai.com/v1";
        var model = await settings.GetAsync(SettingKeys.OpenAiRealtimeModel, "gpt-realtime", ct) ?? "gpt-realtime";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("OpenAI API key not configured; cannot handle realtime call.");
            return;
        }

        var instructions = BuildInstructions(sp.User.BrandName, kbText, fallback);
        var transcript = new StringBuilder();
        var answeredFromKb = true;

        await using var realtime = new OpenAiRealtimeClient(apiKey!, baseUrl, model, _logger);

        realtime.OnAudioDelta += async pcm24k =>
        {
            var slin8k = AudioResampler.Downsample24kTo8k(pcm24k);
            await AudioSocketProtocol.WriteAudioAsync(stream, slin8k, ct);
        };
        realtime.OnAssistantText += text =>
        {
            transcript.Append(text);
            if (!string.IsNullOrEmpty(fallback) && transcript.ToString().Contains(fallback[..Math.Min(15, fallback.Length)]))
                answeredFromKb = false;
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
        await LogCallAsync(sp.Id, startedAt, (int)sw.Elapsed.TotalSeconds, answeredFromKb, transcript.ToString());
    }

    private static string BuildInstructions(string? brand, string kbText, string fallback) => $"""
        تو دستیار صوتی هوشمند برند «{brand}» هستی و به فارسی، مؤدب و کوتاه پاسخ می‌دهی.
        فقط و فقط بر اساس «پایگاه دانش» زیر پاسخ بده. اگر پاسخ سوالِ تماس‌گیرنده در پایگاه
        دانش وجود نداشت، دقیقاً و بدون تغییر این جمله را بگو: «{fallback}» و چیز دیگری اضافه نکن.

        === پایگاه دانش ===
        {kbText}
        === پایان پایگاه دانش ===
        """;

    private async Task LogCallAsync(int smartPhoneId, DateTime startedAt, int durationSeconds, bool answeredFromKb, string transcript)
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
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log call session.");
        }
    }
}
