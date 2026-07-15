using System.Text;
using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// فراخوانِ سرویسِ arka-voice روی Isabel: {phone, text} را می‌فرستد تا با صدای گنجی
/// به کاربر زنگ بزند و متن را بخواند. آدرس/سکرت از تنظیماتِ برنامه خوانده می‌شود.
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

    public async Task<bool> CallAndSpeakAsync(string phoneNumber, string text, CancellationToken ct = default)
    {
        var url = _config["VoiceService:Url"];
        var secret = _config["VoiceService:Secret"];
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("VoiceService:Url پیکربندی نشده؛ تماس صوتی انجام نشد. متن: {Text}", text);
            return false;
        }
        try
        {
            // StringContent با Content-Length صریح (سرویسِ ساده‌ی مقصد chunked را نمی‌فهمد).
            var json = JsonSerializer.Serialize(new { secret, phone = phoneNumber, text });
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("arka-voice error {Status}: {Body}", res.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "arka-voice call failed for {Phone}", phoneNumber);
            return false;
        }
    }
}
