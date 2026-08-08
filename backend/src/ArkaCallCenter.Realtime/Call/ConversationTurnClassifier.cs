using System.Text;
using System.Text.RegularExpressions;

namespace ArkaCallCenter.Realtime.Call;

/// <summary>
/// نوبت‌های اجتماعی کوتاه را از پرسش‌های دانشی جدا می‌کند. احوال‌پرسی و تشکر نباید
/// وارد RAG شوند یا در گزارش «سؤال بی‌پاسخ» ثبت شوند.
/// </summary>
public static class ConversationTurnClassifier
{
    private static readonly HashSet<string> GreetingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "سلام", "درود", "الو"
    };

    private static readonly HashSet<string> GreetingFillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "عرض", "ادب", "خدمت", "وقت", "صبح", "ظهر", "عصر", "شب", "بخیر", "خوش",
        "آمدید", "خوبی", "خوبید", "هستید", "خسته", "نباشید"
    };

    private static readonly HashSet<string> ThanksWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ممنون", "متشکرم", "مرسی", "سپاس", "سپاسگزارم", "تشکر"
    };

    private static readonly HashSet<string> GoodbyeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "خداحافظ", "بدرود", "خدانگهدار", "خدا", "نگهدار", "فعلا", "فعلاً"
    };

    private static readonly HashSet<string> AcknowledgmentWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "باشه", "حتما", "حتماً", "اوکی", "فهمیدم", "متوجه", "شدم", "بسیار", "خوب"
    };

    private static readonly HashSet<string> PoliteFillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "خیلی", "زیاد", "از", "بر", "شما", "لطف", "دارید", "کنید"
    };

    public static bool TryCreateResponse(string text, out string response)
    {
        response = "";
        var tokens = Tokenize(text);
        if (tokens.Count == 0 || tokens.Count > 8) return false;

        if (ContainsOnlyIntent(tokens, GoodbyeWords, PoliteFillers))
        {
            response = "خدانگهدار، روز خوبی داشته باشید.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, ThanksWords, PoliteFillers))
        {
            response = "خواهش می‌کنم. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        var greetingAllowed = GreetingWords
            .Concat(GreetingFillers)
            .Concat(PoliteFillers)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tokens.Any(GreetingWords.Contains) && tokens.All(greetingAllowed.Contains))
        {
            response = "سلام! خوش آمدید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, AcknowledgmentWords, PoliteFillers))
        {
            response = "حتماً. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        return false;
    }

    private static bool ContainsOnlyIntent(
        IReadOnlyCollection<string> tokens,
        HashSet<string> intentWords,
        HashSet<string> allowedFillers)
        => tokens.Any(intentWords.Contains) &&
           tokens.All(token => intentWords.Contains(token) || allowedFillers.Contains(token));

    private static List<string> Tokenize(string text)
    {
        var normalized = (text ?? "")
            .Normalize(NormalizationForm.FormKC)
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Replace("‌", " ")
            .ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[\\u064B-\\u065F\\u0670]", "");

        return Regex.Split(normalized, @"[^\p{L}\p{N}]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }
}
