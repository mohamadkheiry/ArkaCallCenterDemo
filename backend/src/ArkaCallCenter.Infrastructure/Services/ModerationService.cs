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

        // تا دو بار تلاش می‌کنیم؛ خطاهای گذرای شبکه (به‌ویژه از داخل ایران) نباید محتوای سالم را رد کنند.
        Exception? last = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var raw = await _openai.ChatAsync(SystemPrompt, content, jsonMode: true, ct);
                if (TryParse(raw, out var result)) return result;
                _logger.LogWarning("Moderation returned unparsable content (attempt {Attempt}): {Raw}", attempt, Trunc(raw));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                _logger.LogWarning(ex, "Moderation call failed (attempt {Attempt}/2).", attempt);
            }
        }

        _logger.LogError(last, "Moderation failed after retries; rejecting content (fail-closed).");
        return new ModerationResult(false, "بررسی محتوا ممکن نشد؛ لطفاً کمی بعد دوباره تلاش کنید.");
    }

    /// <summary>خواندنِ مقاومِ پاسخِ مدل: مقدارِ allowed می‌تواند boolean یا رشته‌ی «true»/«false» باشد؛
    /// در صورت وجودِ حصارِ ```json نیز پاک‌سازی می‌شود.</summary>
    private static bool TryParse(string? raw, out ModerationResult result)
    {
        result = new ModerationResult(false, "محتوا مغایر با قوانین است.");
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var json = raw.Trim();
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start >= 0 && end > start) json = json[start..(end + 1)];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("allowed", out var a)) return false;
            var allowed = a.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(a.GetString(), out var b) && b,
                _ => false,
            };
            var reason = doc.RootElement.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString() : null;
            result = new ModerationResult(allowed, allowed ? null : (string.IsNullOrWhiteSpace(reason) ? "محتوا مغایر با قوانین است." : reason));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Trunc(string? s) => string.IsNullOrEmpty(s) ? "" : (s.Length <= 200 ? s : s[..200]);
}
