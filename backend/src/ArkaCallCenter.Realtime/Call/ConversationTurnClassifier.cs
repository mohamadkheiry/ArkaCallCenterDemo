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
        "سلام", "درود", "الو", "صبح", "ظهر", "عصر", "شب", "روز", "وقت", "خسته"
    };

    private static readonly HashSet<string> GreetingFillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "عرض", "ادب", "خدمت", "بخیر", "خوش", "آمدید", "نباشید"
    };

    private static readonly HashSet<string> ThanksWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ممنون", "ممنونم", "متشکرم", "مرسی", "سپاس", "سپاسگزارم", "تشکر",
        "دست", "درد", "نکنه", "نکند", "زحمت", "کشیدید"
    };

    private static readonly HashSet<string> GoodbyeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "خداحافظ", "بدرود", "خدانگهدار", "خدا", "نگهدار", "فعلا", "فعلاً"
    };

    private static readonly HashSet<string> AcknowledgmentWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "باشه", "حتما", "حتماً", "اوکی", "فهمیدم", "متوجه", "شدم", "بسیار", "خوب", "درسته", "درست"
    };

    private static readonly HashSet<string> WellbeingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "حال", "حالت", "حالتون", "احوال", "خوبی", "خوبید", "چطوری", "چطورید", "چطوره", "خبر"
    };

    private static readonly HashSet<string> ApologyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ببخشید", "معذرت", "پوزش", "عذر", "شرمنده"
    };

    private static readonly HashSet<string> CommonSocialWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "خیلی", "زیاد", "از", "بر", "به", "با", "و", "که", "من", "شما", "خودت", "خودتون", "خودتان",
        "لطف", "دارید", "کنید", "کردید", "می", "کنم", "هستم", "هستی", "هستید", "هست", "است",
        "چه", "چطور", "چطوره", "چطورید", "بابت", "واقعا", "واقعاً"
    };

    public static bool TryCreateResponse(string text, out string response)
    {
        response = "";
        var tokens = Tokenize(text);
        if (tokens.Count == 0 || tokens.Count > 12) return false;

        var allSocialWords = GreetingWords
            .Concat(GreetingFillers)
            .Concat(ThanksWords)
            .Concat(GoodbyeWords)
            .Concat(AcknowledgmentWords)
            .Concat(WellbeingWords)
            .Concat(ApologyWords)
            .Concat(CommonSocialWords)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isPoliteDayFarewell = tokens.Contains("روز", StringComparer.OrdinalIgnoreCase) &&
                                  tokens.Contains("خوش", StringComparer.OrdinalIgnoreCase) &&
                                  tokens.All(token => allSocialWords.Contains(token) || token == "روز");
        if (ContainsOnlyIntent(tokens, GoodbyeWords, allSocialWords) || isPoliteDayFarewell)
        {
            response = "خدانگهدار، روز خوبی داشته باشید.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, ThanksWords, allSocialWords))
        {
            response = "خواهش می‌کنم. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, ApologyWords, allSocialWords))
        {
            response = "خواهش می‌کنم، بفرمایید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, WellbeingWords, allSocialWords))
        {
            response = "ممنون، خوبم و با کمال میل آماده‌ام کمکتان کنم.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, GreetingWords, allSocialWords))
        {
            response = "سلام! خوش آمدید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, AcknowledgmentWords, allSocialWords))
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
           tokens.All(allowedFillers.Contains);

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
