using System.Text;
using System.Text.RegularExpressions;

namespace ArkaCallCenter.Realtime.Call;

/// <summary>
/// نوبت‌های اجتماعی کوتاه را از پرسش‌های دانشی جدا می‌کند. احوال‌پرسی و تشکر نباید
/// وارد RAG شوند یا در گزارش «سؤال بی‌پاسخ» ثبت شوند.
/// </summary>
public static class ConversationTurnClassifier
{
    private static readonly HashSet<string> IdentityQuestionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "کجا", "کجاست", "کجای", "گرفتم", "گرفته", "گرفته‌ام", "کردم", "کرده", "کرده‌ام",
        "نام", "اسم", "چیست", "چیه", "کی", "هستید"
    };

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

    private static readonly HashSet<string> AffirmationWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "بله", "بلی", "آره", "اره", "آها"
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
        "چه", "چطور", "چطوره", "چطورید", "بابت", "واقعا", "واقعاً", "بفرما", "بفرمایید"
    };

    public static bool TryCreateBusinessIdentityResponse(string text, string? brandName, out string response)
    {
        response = "";
        var tokens = Tokenize(text);
        if (tokens.Count == 0 || tokens.Count > 16) return false;

        var asksAboutCallDestination = tokens.Contains("تماس", StringComparer.OrdinalIgnoreCase) &&
                                       tokens.Any(IdentityQuestionWords.Contains);
        var asksWhereThisIs = (tokens.Contains("اینجا", StringComparer.OrdinalIgnoreCase) ||
                               (tokens.Contains("این", StringComparer.OrdinalIgnoreCase) &&
                                tokens.Contains("جا", StringComparer.OrdinalIgnoreCase))) &&
                              tokens.Any(token => token is "کجا" or "کجاست" or "کجای");
        var hasSecondPersonCopula = tokens.Any(token => token is "هستی" or "هستید" or "هستین");
        var asksWhoYouAre =
            // In Persian the second-person verb already identifies the addressee, so
            // callers commonly omit «شما» and simply say «کی هستی؟».
            (tokens.Contains("کی", StringComparer.OrdinalIgnoreCase) && hasSecondPersonCopula) ||
            (tokens.Contains("چه", StringComparer.OrdinalIgnoreCase) &&
             tokens.Contains("کسی", StringComparer.OrdinalIgnoreCase) && hasSecondPersonCopula) ||
            (tokens.Any(token => token is "اسمت" or "نامت" or "اسمتون" or "نامتون" or "اسمتان" or "نامتان") &&
             tokens.Any(token => token is "چیست" or "چیه")) ||
            (tokens.Contains("شما", StringComparer.OrdinalIgnoreCase) &&
             tokens.Any(token => token is "نام" or "اسم") &&
             tokens.Any(token => token is "هست" or "هستید" or "چیست" or "چیه")) ||
            (tokens.Contains("معرفی", StringComparer.OrdinalIgnoreCase) &&
             tokens.Any(token => token is "خودت" or "خودتو" or "خودتون" or "خودتان") &&
             tokens.Any(token => token is "کن" or "کنید" or "بکن" or "بکنید"));
        var asksBusinessName = (tokens.Contains("کسب", StringComparer.OrdinalIgnoreCase) ||
                                tokens.Contains("مجموعه", StringComparer.OrdinalIgnoreCase) ||
                                tokens.Contains("شرکت", StringComparer.OrdinalIgnoreCase)) &&
                               (tokens.Any(token => token is "نام" or "اسم") ||
                                tokens.Any(token => token is "چه" or "کدام"));

        if (!asksAboutCallDestination && !asksWhereThisIs && !asksWhoYouAre && !asksBusinessName)
            return false;

        response = string.IsNullOrWhiteSpace(brandName)
            ? "شما با این مجموعه تماس گرفته‌اید."
            : $"شما با کسب‌وکار «{brandName.Trim()}» تماس گرفته‌اید.";
        return true;
    }

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
            .Concat(AffirmationWords)
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

        if (ContainsOnlyIntent(tokens, AffirmationWords, allSocialWords))
        {
            response = "بله، بفرمایید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, AcknowledgmentWords, allSocialWords))
        {
            response = "حتماً. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        return false;
    }

    public static bool HasMeaningfulInput(string text) => Tokenize(text).Count > 0;

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
