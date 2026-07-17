using System.Net.Http.Json;
using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// اعلامِ کاربرانِ جدیدِ دمو در کانالِ بله.
///
/// قراردادِ بله (سازگار با Bot API تلگرام):
///   POST {baseUrl}/bot{token}/sendMessage   با بدنه‌ی {chat_id, text}
///   پاسخ: {"ok":true,...} یا {"ok":false,"error_code":..,"description":".."}
///   موفقیت از فیلدِ «ok» خوانده می‌شود.
///
/// حداکثر سه پیام برای هر کاربر (هر مرحله یک‌بار)؛ یکتاییِ (شماره، مرحله) در دیتابیس تضمین می‌شود.
/// </summary>
public class BaleNotifier : IBaleNotifier
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<BaleNotifier> _logger;

    public BaleNotifier(IServiceScopeFactory scopes, IHttpClientFactory http, ILogger<BaleNotifier> logger)
    {
        _scopes = scopes;
        _http = http;
        _logger = logger;
    }

    public void Enqueue(LeadStage stage, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        _ = Task.Run(async () =>
        {
            try { await PostAsync(stage, phoneNumber.Trim()); }
            catch (Exception ex) { _logger.LogWarning(ex, "Bale post failed (stage {Stage}).", stage); }
        });
    }

    public async Task<(bool ok, string? error)> SendTestAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var cfg = await ReadConfigAsync(settings);
        if (cfg is null) return (false, "توکن ربات یا آی‌دی کانال تنظیم نشده است.");
        var text = "‏✅ پیام آزمایشی از «دموی کال سنتر هوشمند آرکا».\nاتصالِ ربات به کانال درست کار می‌کند.";
        return await SendAsync(cfg.Value.baseUrl, cfg.Value.token, cfg.Value.channel, text, ct);
    }

    private async Task PostAsync(LeadStage stage, string phone)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        if ((await settings.GetAsync(SettingKeys.BaleEnabled, "false")) != "true") return;

        // این مرحله قبلاً با موفقیت اعلام شده؟ → دوباره نفرست (سقفِ سه پیام برای هر کاربر).
        if (await db.BaleChannelPosts.AnyAsync(x => x.PhoneNumber == phone && x.Stage == stage && x.Success))
            return;

        var cfg = await ReadConfigAsync(settings);
        if (cfg is null)
        {
            _logger.LogWarning("Bale post skipped: botToken/channelId not configured.");
            return;
        }

        var user = await db.Users.AsNoTracking()
            .Include(u => u.SmartPhone)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phone);

        var text = BuildMessage(stage, user, phone);
        var (ok, error) = await SendAsync(cfg.Value.baseUrl, cfg.Value.token, cfg.Value.channel, text, default);

        if (ok) _logger.LogInformation("Bale posted (stage {Stage}, phone {Phone}).", stage, phone);
        else _logger.LogWarning("Bale post rejected (stage {Stage}, phone {Phone}): {Err}", stage, phone, error);

        try
        {
            var existing = await db.BaleChannelPosts.FirstOrDefaultAsync(x => x.PhoneNumber == phone && x.Stage == stage);
            if (existing is null)
            {
                db.BaleChannelPosts.Add(new BaleChannelPost
                {
                    PhoneNumber = phone,
                    Stage = stage,
                    Success = ok,
                    ResponseMessage = Trunc(error),
                    SentAt = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Success = ok;
                existing.ResponseMessage = Trunc(error);
                existing.SentAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // رقابتِ همزمان روی ایندکسِ یکتا → یعنی مرحله ثبت شده؛ بی‌خطر است.
        }
    }

    private static async Task<(string baseUrl, string token, string channel)?> ReadConfigAsync(ISettingsService settings)
    {
        var baseUrl = (await settings.GetAsync(SettingKeys.BaleBaseUrl, "https://tapi.bale.ai"))?.TrimEnd('/');
        var token = (await settings.GetAsync(SettingKeys.BaleBotToken, null))?.Trim();
        var channel = (await settings.GetAsync(SettingKeys.BaleChannelId, null))?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(channel))
            return null;
        // آی‌دیِ پابلیکِ کانال باید با @ شروع شود؛ اگر ادمین بدونِ @ وارد کرد، خودمان اضافه می‌کنیم.
        if (!channel.StartsWith('@') && !channel.StartsWith('-')) channel = "@" + channel;
        return (baseUrl, token, channel);
    }

    private async Task<(bool ok, string? error)> SendAsync(string baseUrl, string token, string channel, string text, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient("bale");
            using var res = await client.PostAsJsonAsync(
                $"{baseUrl}/bot{token}/sendMessage",
                new { chat_id = channel, text }, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            // موفقیت از فیلدِ ok خوانده می‌شود (نه فقط status code).
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True)
                    return (true, null);
                var desc = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString() : Trunc(body);
                return (false, desc);
            }
            catch (JsonException)
            {
                return (false, $"HTTP {(int)res.StatusCode}: {Trunc(body)}");
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>متنِ پیامِ کانال برای هر مرحله.</summary>
    private static string BuildMessage(LeadStage stage, User? user, string phone)
    {
        var name = $"{user?.FirstName} {user?.LastName}".Trim();
        var lines = new List<string>();
        switch (stage)
        {
            case LeadStage.PhoneEntered:
                lines.Add("‏🆕 کاربر جدید وارد دمو شد");
                lines.Add($"‏📱 موبایل: {phone}");
                break;
            case LeadStage.ProfileCompleted:
                lines.Add("‏👤 کاربر نام خود را ثبت کرد");
                lines.Add($"‏📱 موبایل: {phone}");
                if (!string.IsNullOrWhiteSpace(name)) lines.Add($"‏🔖 نام: {name}");
                if (!string.IsNullOrWhiteSpace(user?.BrandName)) lines.Add($"‏🏢 برند: {user!.BrandName}");
                break;
            case LeadStage.SmartPhoneCreated:
                lines.Add("‏☎️ کاربر تلفن هوشمند ساخت");
                lines.Add($"‏📱 موبایل: {phone}");
                if (!string.IsNullOrWhiteSpace(name)) lines.Add($"‏🔖 نام: {name}");
                if (!string.IsNullOrWhiteSpace(user?.BrandName)) lines.Add($"‏🏢 برند: {user!.BrandName}");
                if (user?.SmartPhone?.Extension is not null) lines.Add($"‏🔢 شماره داخلی: {user.SmartPhone.Extension}");
                break;
        }
        lines.Add("‏— دموی کال سنتر هوشمند آرکا");
        return string.Join("\n", lines);
    }

    private static string? Trunc(string? s, int max = 400)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
