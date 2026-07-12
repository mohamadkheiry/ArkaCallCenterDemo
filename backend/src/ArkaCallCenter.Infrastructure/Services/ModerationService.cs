using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// بررسی انطباق محتوا با قوانین جمهوری اسلامی ایران با استفاده از یک مدل chat.
/// در صورت خطای سرویس، به‌صورت محتاطانه محتوا را رد می‌کند (fail-closed).
/// </summary>
public class ModerationService : IModerationService
{
    private readonly IOpenAiService _openai;
    private readonly ILogger<ModerationService> _logger;

    public ModerationService(IOpenAiService openai, ILogger<ModerationService> logger)
    {
        _openai = openai;
        _logger = logger;
    }

    private const string SystemPrompt = """
        تو یک ناظر محتوا برای یک سامانه‌ی ایرانی هستی. وظیفه‌ات بررسی این است که آیا متن
        ورودی مغایر با قوانین جمهوری اسلامی ایران است یا خیر. موارد ممنوع شامل:
        محتوای مستهجن یا جنسی، توهین به مقدسات و ادیان، تبلیغ مواد مخدر یا الکل،
        محتوای ضد امنیت ملی یا تجزیه‌طلبانه، تشویق به خشونت و اعمال مجرمانه،
        قمار، و هر محتوای خلاف عفت عمومی.
        فقط یک شیء JSON برگردان با این ساختار:
        {"allowed": true|false, "reason": "دلیل کوتاه فارسی در صورت رد"}
        اگر محتوا مجاز است، allowed=true و reason خالی. زبان reason فارسی باشد.
        """;

    public async Task<ModerationResult> CheckAsync(string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ModerationResult(false, "محتوا خالی است.");

        try
        {
            var raw = await _openai.ChatAsync(SystemPrompt, content, jsonMode: true, ct);
            using var doc = JsonDocument.Parse(raw);
            var allowed = doc.RootElement.TryGetProperty("allowed", out var a) && a.ValueKind == JsonValueKind.True;
            var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : null;
            return new ModerationResult(allowed, allowed ? null : (reason ?? "محتوا مغایر با قوانین است."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Moderation failed; rejecting content (fail-closed).");
            return new ModerationResult(false, "بررسی محتوا ممکن نشد؛ لطفاً دوباره تلاش کنید.");
        }
    }
}
