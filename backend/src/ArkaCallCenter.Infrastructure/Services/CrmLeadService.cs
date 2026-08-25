using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
/// قراردادِ عملیاتی سرویس:
///   POST {baseUrl}/api/User/Login با username/password برای گرفتن Bearer token
///   POST {baseUrl}/api/ContactUs/InsertContactUsByAdmin با multipart/form-data
///   فیلدهای الزامی فرم: inputModel.Name، Email، PhoneNumber و FeedbackText.
///   موفقیت علاوه بر status code از فیلد «success» پاسخ کنترل می‌شود.
///
/// چون سامانه‌ی ما ایمیل نمی‌گیرد ولی CRM آن را الزامی می‌داند، ایمیلِ جایگزین از روی
/// شماره ساخته می‌شود (مثلاً 09121234567@demo.arkadp.com) تا لید از دست نرود.
/// </summary>
public class CrmLeadService : ICrmLeadService
{
    internal const string LoginPath = "/api/User/Login";
    internal const string InsertLeadPath = "/api/ContactUs/InsertContactUsByAdmin";

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
        var username = await settings.GetAsync(SettingKeys.CrmUsername, null);
        var password = await settings.GetAsync(SettingKeys.CrmPassword, null);
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("CRM lead skipped: baseUrl/username/password not configured.");
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

        var client = _http.CreateClient("crm");
        bool ok = false;
        string? message = null;
        try
        {
            var (token, loginError) = await LoginAsync(client, baseUrl, username, password);
            if (string.IsNullOrWhiteSpace(token))
            {
                message = loginError ?? "CRM login did not return a token.";
            }
            else
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + InsertLeadPath)
                {
                    Content = CreateLeadContent(name, email, phone, feedback),
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var res = await client.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                (ok, message) = ParseResult(body);
                if (!res.IsSuccessStatusCode)
                {
                    ok = false;
                    message = $"HTTP {(int)res.StatusCode}: {Trunc(body)}";
                }
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

    private static async Task<(string? token, string? error)> LoginAsync(
        HttpClient client,
        string baseUrl,
        string username,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            baseUrl + LoginPath,
            new { username, password });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (null, $"CRM login HTTP {(int)response.StatusCode}: {Trunc(body)}");

        return TryParseLoginToken(body, out var token)
            ? (token, null)
            : (null, "CRM login response had no token.");
    }

    internal static MultipartFormDataContent CreateLeadContent(
        string name,
        string email,
        string phoneNumber,
        string feedbackText)
    {
        var content = new MultipartFormDataContent();
        AddText(content, "inputModel.Name", name);
        AddText(content, "inputModel.Email", email);
        AddText(content, "inputModel.PhoneNumber", phoneNumber);
        AddText(content, "inputModel.FeedbackText", feedbackText);
        AddText(content, "inputModel.RequestType", "2");               // ProjectImplementationRequest
        AddText(content, "inputModel.RequestSource", "2");             // CallCenter
        AddText(content, "inputModel.RequestedProject", "1");          // SmartCallCenter
        AddText(content, "inputModel.FormStatus", "1");                // New
        return content;
    }

    private static void AddText(MultipartFormDataContent content, string fieldName, string value)
        => content.Add(new StringContent(value, Encoding.UTF8), fieldName);

    internal static bool TryParseLoginToken(string body, out string? token)
    {
        token = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!TryGetPropertyIgnoreCase(document.RootElement, "result", out var result) ||
                result.ValueKind != JsonValueKind.Object ||
                !TryGetPropertyIgnoreCase(result, "token", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String)
                return false;

            token = tokenElement.GetString()?.Trim();
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>خواندنِ {"success":bool,"message":string} از پاسخ.</summary>
    internal static (bool ok, string? message) ParseResult(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ok = TryGetPropertyIgnoreCase(root, "success", out var s) && s.ValueKind == JsonValueKind.True;
            var msg = TryGetPropertyIgnoreCase(root, "message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() : null;
            return (ok, msg);
        }
        catch (JsonException)
        {
            return (false, Trunc(body));
        }
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }

        value = default;
        return false;
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
