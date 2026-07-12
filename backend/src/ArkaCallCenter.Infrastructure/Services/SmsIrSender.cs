using System.Net.Http.Json;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// ارسال پیامک از طریق SMS.ir (REST v1). اگر apiKey/lineNumber در تنظیمات
/// سوپرادمین پیکربندی نشده باشد، پیامک فقط لاگ می‌شود (حالت توسعه).
/// </summary>
public class SmsIrSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly ILogger<SmsIrSender> _logger;

    public SmsIrSender(HttpClient http, ISettingsService settings, ILogger<SmsIrSender> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string phoneNumber, string text, CancellationToken ct = default)
    {
        var apiKey = await _settings.GetAsync(SettingKeys.SmsIrApiKey, null, ct);
        var lineNumber = await _settings.GetAsync(SettingKeys.SmsIrLineNumber, null, ct);

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(lineNumber))
        {
            _logger.LogInformation("[SMS(dev)→{Phone}] {Text}", phoneNumber, text);
            return true;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.sms.ir/v1/send/bulk")
            {
                Content = JsonContent.Create(new
                {
                    lineNumber = long.TryParse(lineNumber, out var ln) ? ln : 0,
                    messageText = text,
                    mobiles = new[] { phoneNumber },
                }),
            };
            req.Headers.Add("X-API-KEY", apiKey);
            req.Headers.Add("Accept", "application/json");

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("SMS.ir error {Status}: {Body}", res.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS.ir send failed for {Phone}", phoneNumber);
            return false;
        }
    }
}
