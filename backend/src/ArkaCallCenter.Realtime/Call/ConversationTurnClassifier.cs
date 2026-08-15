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
        "سلام", "درود", "الو"
    };

    private static readonly HashSet<string> GreetingFillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "صبح", "ظهر", "عصر", "شب", "روز", "روزتون", "روزتان", "وقت", "وقتتون", "وقتتان", "خسته",
        "عرض", "ادب", "احترام", "خدمت", "بخیر", "خوش", "آمدید", "نباشید", "علیکم", "علیک", "قوت"
    };

    private static readonly HashSet<string> ThanksWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ممنون", "ممنونم", "ممنونیم", "متشکر", "متشکرم", "متشکریم", "مرسی", "سپاس", "سپاسگزار", "سپاسگزارم", "سپاسگزاریم", "تشکر",
        "دمت"
    };

    private static readonly HashSet<string> ThanksContextWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "دست", "دستت", "دستتون", "دستتان", "درد", "نکنه", "نکند", "زحمت", "کشیدی", "کشیدین", "کشیدید",
        "لطف", "لطفت", "لطفتون", "لطفتان", "محبت", "کردی", "کردین", "کردید", "گرم", "فراوان",
        "بابت", "برای", "این", "همه", "را", "رو", "راهنمایی", "راهنماییتون", "راهنماییتان", "توضیح", "توضیحات",
        "پاسخ", "پاسختون", "پاسختان", "جواب", "کمک", "کمکتون", "کمکتان", "وقتی", "وقتتون", "وقتتان",
        "خدا", "خیرتون", "خیرتان", "بده"
    };

    private static readonly HashSet<string> GoodbyeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "خداحافظ", "بدرود", "خدانگهدار", "خدانگهدارتون", "خدانگهدارتان", "فعلا", "فعلاً"
    };

    private static readonly HashSet<string> GoodbyeContextWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "خدا", "حافظ", "نگهدار", "موفق", "پیروز", "دیدار", "امید", "یا", "علی",
        "روز", "روزتون", "روزتان", "خوش", "باشید"
    };

    private static readonly HashSet<string> AcknowledgmentWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "باشه", "باشد", "حتما", "حتماً", "اوکی", "اوکیه", "فهمیدم", "متوجه", "شدم", "بسیار", "خوب", "درسته", "درست", "صحیح",
        "خواهش", "قابلی", "عالی", "قبوله", "خب"
    };

    private static readonly HashSet<string> AffirmationWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "بله", "بلی", "آره", "اره", "آها", "آری"
    };

    private static readonly HashSet<string> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "نه", "خیر", "نخیر"
    };

    private static readonly HashSet<string> WellbeingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "حال", "حالت", "حالتون", "حالتان", "احوال", "خوبی", "خوبید", "خوبم", "چطوری", "چطورید", "چطوره", "خبر", "سلامتی", "اوضاع"
    };

    private static readonly HashSet<string> ApologyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ببخشید", "ببخشین", "معذرت", "پوزش", "عذر", "شرمنده", "مزاحم"
    };

    private static readonly HashSet<string> CommonSocialWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "خیلی", "زیاد", "از", "بر", "به", "با", "و", "که", "من", "شما", "خودت", "خودتون", "خودتان",
        "دارید", "کنید", "می", "کنم", "میکنم", "میخوام", "میخواهم", "هستم", "هستی", "هستید", "هست", "است",
        "بود", "بودید", "باشید", "نداشت", "دیگه", "چه", "چطور", "چطوره", "چطورید", "تون", "تان", "واقعا", "واقعاً",
        "زنده", "بفرما", "بفرمایید"
    };

    private static readonly HashSet<string> ApologyContextWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "وقت", "وقتتون", "وقتتان", "را", "رو", "گرفتم"
    };

    private static readonly HashSet<string> SimpleSocialWords = MergeWords(
        GreetingWords,
        ThanksWords,
        GoodbyeWords,
        AcknowledgmentWords,
        AffirmationWords,
        NegativeWords,
        WellbeingWords,
        ApologyWords,
        CommonSocialWords);

    private static readonly HashSet<string> GreetingAllowedWords = MergeWords(SimpleSocialWords, GreetingFillers);
    private static readonly HashSet<string> ThanksAllowedWords = MergeWords(SimpleSocialWords, ThanksContextWords);
    private static readonly HashSet<string> GoodbyeAllowedWords = MergeWords(SimpleSocialWords, GoodbyeContextWords);
    private static readonly HashSet<string> ApologyAllowedWords = MergeWords(SimpleSocialWords, ApologyContextWords);

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

        var isEncouragementGreeting = tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase) &&
                                      tokens.Contains("قوت", StringComparer.OrdinalIgnoreCase) &&
                                      tokens.All(token => GreetingAllowedWords.Contains(token) || token == "خدا");
        if (isEncouragementGreeting)
        {
            response = "سلامت باشید! خوش آمدید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        var isPoliteDayFarewell = tokens.Any(token => token is "روز" or "روزتون" or "روزتان") &&
                                  tokens.Contains("خوش", StringComparer.OrdinalIgnoreCase) &&
                                  tokens.All(GoodbyeAllowedWords.Contains);
        var isSuccessfulFarewell = tokens.Any(token => token is "موفق" or "پیروز") &&
                                   tokens.Contains("باشید", StringComparer.OrdinalIgnoreCase) &&
                                   tokens.All(GoodbyeAllowedWords.Contains);
        var isHopeToSeeYouFarewell = tokens.Contains("امید", StringComparer.OrdinalIgnoreCase) &&
                                     tokens.Contains("دیدار", StringComparer.OrdinalIgnoreCase) &&
                                     tokens.All(GoodbyeAllowedWords.Contains);
        var isSeparatedGoodbye = tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase) &&
                                 tokens.Any(token => token is "حافظ" or "نگهدار") &&
                                 tokens.All(GoodbyeAllowedWords.Contains);
        var isYaAliFarewell = tokens.Contains("یا", StringComparer.OrdinalIgnoreCase) &&
                              tokens.Contains("علی", StringComparer.OrdinalIgnoreCase) &&
                              tokens.All(GoodbyeAllowedWords.Contains);
        if (ContainsOnlyIntent(tokens, GoodbyeWords, GoodbyeAllowedWords) || isPoliteDayFarewell ||
            isSuccessfulFarewell || isHopeToSeeYouFarewell || isSeparatedGoodbye || isYaAliFarewell)
        {
            response = "خدانگهدار، روز خوبی داشته باشید.";
            return true;
        }

        var isPoliteAppreciation = tokens.Any(token => token is "لطف" or "محبت") &&
                                   tokens.Any(token => token is "کردی" or "کردین" or "کردید") &&
                                   tokens.All(ThanksAllowedWords.Contains);
        var isHandPainThanks = tokens.Any(token => token is "دست" or "دستت" or "دستتون" or "دستتان") &&
                               tokens.Contains("درد", StringComparer.OrdinalIgnoreCase) &&
                               tokens.Any(token => token is "نکنه" or "نکند") &&
                               tokens.All(ThanksAllowedWords.Contains);
        var isEffortThanks = tokens.Contains("زحمت", StringComparer.OrdinalIgnoreCase) &&
                             tokens.Any(token => token is "کشیدی" or "کشیدین" or "کشیدید") &&
                             tokens.All(ThanksAllowedWords.Contains);
        var isDivineThanks = tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase) &&
                             tokens.Any(token => token is "خیرتون" or "خیرتان") &&
                             tokens.Contains("بده", StringComparer.OrdinalIgnoreCase) &&
                             tokens.All(ThanksAllowedWords.Contains);
        if (ContainsOnlyIntent(tokens, ThanksWords, ThanksAllowedWords) || isPoliteAppreciation ||
            isHandPainThanks || isEffortThanks || isDivineThanks)
        {
            response = "خواهش می‌کنم. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, ApologyWords, ApologyAllowedWords))
        {
            response = "خواهش می‌کنم، بفرمایید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        var asksHowYouAre = tokens.Contains("خوب", StringComparer.OrdinalIgnoreCase) &&
                            tokens.Any(token => token is "هستی" or "هستید") &&
                            tokens.All(SimpleSocialWords.Contains);
        if (ContainsOnlyIntent(tokens, WellbeingWords, SimpleSocialWords) || asksHowYouAre)
        {
            response = "ممنون، خوبم و با کمال میل آماده‌ام کمکتان کنم.";
            return true;
        }

        var isTimeGreeting = tokens.Any(token => token is "صبح" or "ظهر" or "عصر" or "شب" or "روز" or "روزتون" or "روزتان" or "وقت" or "وقتتون" or "وقتتان") &&
                             tokens.Contains("بخیر", StringComparer.OrdinalIgnoreCase) &&
                             tokens.All(GreetingAllowedWords.Contains);
        var isTiredGreeting = tokens.Contains("خسته", StringComparer.OrdinalIgnoreCase) &&
                              tokens.Contains("نباشید", StringComparer.OrdinalIgnoreCase) &&
                              tokens.All(GreetingAllowedWords.Contains);
        var isFormalGreeting = tokens.Contains("عرض", StringComparer.OrdinalIgnoreCase) &&
                               tokens.Any(token => token is "ادب" or "احترام" or "سلام") &&
                               tokens.All(GreetingAllowedWords.Contains);
        if (ContainsOnlyIntent(tokens, GreetingWords, GreetingAllowedWords) || isTimeGreeting || isTiredGreeting || isFormalGreeting)
        {
            response = "سلام! خوش آمدید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, AffirmationWords, SimpleSocialWords))
        {
            response = "بله، بفرمایید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, NegativeWords, SimpleSocialWords))
        {
            response = "بسیار خوب. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, AcknowledgmentWords, SimpleSocialWords))
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

    private static HashSet<string> MergeWords(params IEnumerable<string>[] groups)
        => groups.SelectMany(group => group).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
