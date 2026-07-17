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
/// ارسالِ لیدِ کاربرانِ دمو به CRM فروش.
///
/// قراردادِ واقعیِ سرویس (از روی Swagger و آزمایشِ عملی استخراج شد):
///   POST {baseUrl}/api/ExternalEndpoint/InsertContactUs   با هدرِ X-Api-Key
///   بدنه باید داخلِ «inputModel» بسته‌بندی شود؛ در غیر این صورت سرویس خطای عمومیِ ‎-999 می‌دهد.
///   فیلدهای الزامی: name، email، phoneNumber.
///   پاسخ همیشه HTTP 200 است؛ موفقیت را باید از فیلدِ «success» خواند (نه از status code).
///
/// چون سامانه‌ی ما ایمیل نمی‌گیرد ولی CRM آن را الزامی می‌داند، ایمیلِ جایگزین از روی
/// شماره ساخته می‌شود (مثلاً 09121234567@demo.arkadp.com) تا لید از دست نرود.
/// </summary>
public class CrmLeadService : ICrmLeadService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<CrmLeadService> _logger;

    public CrmLeadService(IServiceScopeFactory scopes, IHttpClientFactory http, ILogger<CrmLeadService> logger)
    {
        _scopes = scopes;
        _http = http;
        _logger = logger;
    }

    /// <summary>«آتش‌کن‌و‌فراموش‌کن»: جریانِ کاربر (ورود/پروفایل/ساخت داخلی) نباید منتظرِ CRM بماند.</summary>
    public void Enqueue(LeadStage stage, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        _ = Task.Run(async () =>
        {
            try { await SubmitAsync(stage, phoneNumber.Trim()); }
            catch (Exception ex) { _logger.LogWarning(ex, "CRM lead submit failed (stage {Stage}).", stage); }
        });
    }

    private async Task SubmitAsync(LeadStage stage, string phone)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        if ((await settings.GetAsync(SettingKeys.CrmEnabled, "false")) != "true") return;

        // این مرحله قبلاً با موفقیت ارسال شده؟ → دوباره نفرست.
        if (await db.CrmLeadSubmissions.AnyAsync(x => x.PhoneNumber == phone && x.Stage == stage && x.Success))
            return;

        var baseUrl = (await settings.GetAsync(SettingKeys.CrmBaseUrl, "https://api.arkadp.com"))?.TrimEnd('/');
        var apiKey = await settings.GetAsync(SettingKeys.CrmApiKey, null);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("CRM lead skipped: baseUrl/apiKey not configured.");
            return;
        }
        var emailDomain = (await settings.GetAsync(SettingKeys.CrmEmailDomain, "demo.arkadp.com"))?.Trim() ?? "demo.arkadp.com";

        var user = await db.Users.AsNoTracking()
            .Include(u => u.SmartPhone)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phone);

        var name = BuildName(user, phone);
        var feedback = await BuildFeedbackAsync(stage, user, phone, db);
        // CRM ایمیل را الزامی می‌داند و ما ایمیل نداریم → ایمیلِ جایگزین از روی شماره.
        var email = $"{new string(phone.Where(char.IsDigit).ToArray())}@{emailDomain}";

        var payload = new
        {
            inputModel = new
            {
                name,
                email,
                phoneNumber = phone,
                feedbackText = feedback,
                requestedProject = 1,   // 1 = SmartCallCenter
                requestType = 2,        // 2 = ProjectImplementationRequest
            },
        };

        var client = _http.CreateClient("crm");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/ExternalEndpoint/InsertContactUs")
        {
            Content = JsonContent.Create(payload),
        };
        req.Headers.Add("X-Api-Key", apiKey);

        bool ok = false;
        string? message = null;
        try
        {
            using var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            // پاسخ همیشه 200 است؛ موفقیت از فیلدِ success خوانده می‌شود.
            (ok, message) = ParseResult(body);
            if (!res.IsSuccessStatusCode)
            {
                ok = false;
                message = $"HTTP {(int)res.StatusCode}: {Trunc(body)}";
            }
        }
        catch (Exception ex)
        {
            message = ex.Message;
        }

        if (ok)
            _logger.LogInformation("CRM lead sent (stage {Stage}, phone {Phone}).", stage, phone);
        else
            _logger.LogWarning("CRM lead rejected (stage {Stage}, phone {Phone}): {Msg}", stage, phone, message);

        // نتیجه را ثبت کن تا مرحله‌ی موفق دوباره ارسال نشود (و ناموفق‌ها قابل عیب‌یابی باشند).
        try
        {
            var existing = await db.CrmLeadSubmissions.FirstOrDefaultAsync(x => x.PhoneNumber == phone && x.Stage == stage);
            if (existing is null)
            {
                db.CrmLeadSubmissions.Add(new CrmLeadSubmission
                {
                    PhoneNumber = phone,
                    Stage = stage,
                    Success = ok,
                    ResponseMessage = Trunc(message),
                    SentAt = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Success = ok;
                existing.ResponseMessage = Trunc(message);
                existing.SentAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // رقابتِ همزمان روی ایندکسِ یکتا → یعنی مرحله ثبت شده؛ بی‌خطر است.
        }
    }

    /// <summary>خواندنِ {"success":bool,"message":string} از پاسخ.</summary>
    private static (bool ok, string? message) ParseResult(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            var msg = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() : null;
            return (ok, msg);
        }
        catch (JsonException)
        {
            return (false, Trunc(body));
        }
    }

    private static string BuildName(User? user, string phone)
    {
        var full = $"{user?.FirstName} {user?.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(full))
            return string.IsNullOrWhiteSpace(user?.BrandName) ? full : $"{full} ({user!.BrandName})";
        // مرحله‌ی اول: هنوز نامی نداریم ولی CRM «نام» را الزامی می‌داند.
        return $"کاربر دمو {phone}";
    }

    private static async Task<string> BuildFeedbackAsync(LeadStage stage, User? user, string phone, ArkaDbContext db)
    {
        var lines = new List<string> { "لید از «دموی کال سنتر هوشمند آرکا»." };
        switch (stage)
        {
            case LeadStage.PhoneEntered:
                lines.Add("مرحله: شماره‌ی موبایل وارد شد (ورود به دمو).");
                break;
            case LeadStage.ProfileCompleted:
                lines.Add("مرحله: پروفایل تکمیل شد (نام و نام‌خانوادگی).");
                if (!string.IsNullOrWhiteSpace(user?.BrandName)) lines.Add($"برند: {user!.BrandName}");
                break;
            case LeadStage.SmartPhoneCreated:
                lines.Add("مرحله: تلفن هوشمند (داخلی) ساخته شد.");
                var sp = user?.SmartPhone;
                if (sp?.Extension is not null) lines.Add($"شماره داخلی: {sp.Extension}");
                if (!string.IsNullOrWhiteSpace(sp?.WelcomeMessageText))
                    lines.Add($"پیام خوش‌آمد: {Trunc(sp!.WelcomeMessageText, 160)}");
                var kb = await db.KnowledgeBases.AsNoTracking()
                    .FirstOrDefaultAsync(k => user != null && k.UserId == user.Id);
                if (kb is not null) lines.Add($"پایگاه دانش: {kb.CharCount} کاراکتر (وضعیت: {kb.ModerationStatus}).");
                break;
        }
        lines.Add($"موبایل: {phone}");
        return string.Join(" | ", lines);
    }

    private static string? Trunc(string? s, int max = 400)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
