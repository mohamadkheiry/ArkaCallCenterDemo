using System.Text;
using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// فراخوانِ سرویسِ arka-voice روی Isabel برای تماس صوتی: متن (با piper) یا کدِ OTP
/// (با فایل‌های صوتیِ ضبط‌شده). آدرس/سکرت از تنظیماتِ برنامه خوانده می‌شود.
/// </summary>
public class VoiceCaller : IVoiceCaller
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<VoiceCaller> _logger;

    public VoiceCaller(HttpClient http, IConfiguration config, ILogger<VoiceCaller> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    /// <summary>آدرسِ پایه‌ی سرویس (بدونِ مسیر). از VoiceService:Url مشتق می‌شود.</summary>
    private string? BaseUrl()
    {
        var url = _config["VoiceService:Url"];
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.TrimEnd('/');
        if (url.EndsWith("/call")) url = url[..^"/call".Length];
        return url;
    }

    public Task<bool> CallAndSpeakAsync(string phoneNumber, string text, bool rawText = false, CancellationToken ct = default) =>
        PostAsync("/call", new { phone = phoneNumber, text, raw = rawText }, phoneNumber, ct);

    public Task<bool> CallOtpAsync(string phoneNumber, string code, CancellationToken ct = default) =>
        PostAsync("/call-otp", new { phone = phoneNumber, code }, phoneNumber, ct);

    private async Task<bool> PostAsync(string path, object payload, string phone, CancellationToken ct)
    {
        var baseUrl = BaseUrl();
        var secret = _config["VoiceService:Secret"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("VoiceService:Url پیکربندی نشده؛ تماس صوتی انجام نشد.");
            return false;
        }
        try
        {
            // StringContent با Content-Length صریح (سرویسِ ساده‌ی مقصد chunked را نمی‌فهمد).
            var dict = new Dictionary<string, object?> { ["secret"] = secret };
            foreach (var p in payload.GetType().GetProperties())
                dict[p.Name] = p.GetValue(payload);
            var json = JsonSerializer.Serialize(dict);
            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + path)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("arka-voice {Path} error {Status}: {Body}", path, res.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "arka-voice {Path} failed for {Phone}", path, phone);
            return false;
        }
    }
}
