using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Realtime.Realtime;

/// <summary>
/// اتصال به OpenAI Realtime API از طریق WebSocket. صدای caller (PCM16 24kHz) را
/// ارسال و صدای پاسخ + رونوشت متنی را دریافت می‌کند.
/// </summary>
public sealed class OpenAiRealtimeClient : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly ILogger _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public event Func<byte[], Task>? OnAudioDelta;   // PCM16 24kHz
    public event Func<string, Task>? OnAssistantText; // رونوشت پاسخ دستیار (delta)
    public event Func<string, Task>? OnUserTranscript; // رونوشت کامل گفته‌ی کاربر
    public event Func<Task>? OnResponseDone;
    public event Func<Task>? OnUserSpeechStarted;     // کاربر شروع به صحبت کرد → باید AI ساکت شود (barge-in)
    public event Func<Task>? OnUserSpeechStopped;     // کاربر حرفش تمام شد → AI در حال «فکر کردن»
    public event Action<int, int, int>? OnUsage;      // prompt, completion, total

    public OpenAiRealtimeClient(string apiKey, string baseUrl, string model, ILogger logger)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl;
        _model = model;
        _logger = logger;
    }

    public async Task ConnectAsync(string instructions, string voice, CancellationToken ct)
    {
        var host = new Uri(_baseUrl).Host; // مثلاً api.openai.com
        var uri = new Uri($"wss://{host}/v1/realtime?model={_model}");
        _ws.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
        await _ws.ConnectAsync(uri, ct);

        // ساختار GA (نه beta): audio تودرفو + type=realtime. فرمت صوت PCM16 24kHz.
        await SendAsync(new
        {
            type = "session.update",
            session = new
            {
                type = "realtime",
                instructions,
                output_modalities = new[] { "audio" },
                audio = new
                {
                    input = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 },
                        // server VAD: با شروع صحبتِ کاربر، پاسخِ در حال پخشِ AI قطع شود (barge-in)
                        turn_detection = new
                        {
                            type = "server_vad",
                            threshold = 0.5,
                            silence_duration_ms = 600,
                            interrupt_response = true,
                            create_response = true,
                        },
                        transcription = new { model = "whisper-1" },
                    },
                    output = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 },
                        voice,
                    },
                },
            },
        }, ct);

        _ = Task.Run(() => ReceiveLoopAsync(ct), ct);
    }

    /// <summary>درخواست از مدل برای گفتن پیام خوش‌آمد در ابتدای تماس.</summary>
    public Task GreetAsync(string welcomeText, CancellationToken ct) => SendAsync(new
    {
        type = "response.create",
        response = new
        {
            instructions = $"به تماس‌گیرنده این پیام خوش‌آمد را دقیقاً بگو: «{welcomeText}»",
        },
    }, ct);

    public Task AppendAudioAsync(byte[] pcm24k, CancellationToken ct) => SendAsync(new
    {
        type = "input_audio_buffer.append",
        audio = Convert.ToBase64String(pcm24k),
    }, ct);

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1 << 16];
        var sb = new StringBuilder();
        try
        {
            while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                await HandleEventAsync(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Realtime receive loop error"); }
    }

    private async Task HandleEventAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var type = doc.RootElement.GetProperty("type").GetString();
        switch (type)
        {
            // نام‌های GA و beta هر دو پشتیبانی می‌شوند.
            case "response.output_audio.delta":
            case "response.audio.delta":
                if (doc.RootElement.TryGetProperty("delta", out var d) && OnAudioDelta is not null)
                    await OnAudioDelta(Convert.FromBase64String(d.GetString()!));
                break;
            case "response.output_audio_transcript.delta":
            case "response.audio_transcript.delta":
            case "response.output_text.delta":
            case "response.text.delta":
                if (doc.RootElement.TryGetProperty("delta", out var t) && OnAssistantText is not null)
                    await OnAssistantText(t.GetString() ?? "");
                break;
            case "input_audio_buffer.speech_started":
                if (OnUserSpeechStarted is not null) await OnUserSpeechStarted();
                break;
            case "input_audio_buffer.speech_stopped":
                if (OnUserSpeechStopped is not null) await OnUserSpeechStopped();
                break;
            case "conversation.item.input_audio_transcription.completed":
                if (doc.RootElement.TryGetProperty("transcript", out var ut) && OnUserTranscript is not null)
                    await OnUserTranscript(ut.GetString() ?? "");
                break;
            case "response.done":
                TryEmitUsage(doc.RootElement);
                if (OnResponseDone is not null) await OnResponseDone();
                break;
            case "error":
                _logger.LogError("Realtime error: {Json}", json);
                break;
        }
    }

    private void TryEmitUsage(JsonElement root)
    {
        if (OnUsage is null) return;
        if (!root.TryGetProperty("response", out var resp) ||
            !resp.TryGetProperty("usage", out var usage)) return;
        int Get(string n) => usage.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
        var input = Get("input_tokens");
        var output = Get("output_tokens");
        var total = Get("total_tokens");
        if (total == 0) total = input + output;
        OnUsage(input, output, total);
    }

    private async Task SendAsync(object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }
        catch { /* ignore */ }
        _ws.Dispose();
    }
}
