using System.Text;
using System.Text.RegularExpressions;

namespace ArkaCallCenter.Realtime.Call;

/// <summary>
/// نوبت‌های اجتماعی و کنترلی کوتاه تماس را از پرسش‌های دانشی جدا می‌کند.
/// هر نیت، واژگان مجاز خودش را دارد تا وجود یک سلام یا تشکر در ابتدای یک سؤال واقعی،
/// مسیر RAG را دور نزند.
/// </summary>
public static class ConversationTurnClassifier
{
    private static readonly HashSet<string> IdentityQuestionWords = Words(
        "کجا", "کجاست", "کجای", "گرفتم", "گرفته", "گرفته‌ام", "کردم", "کرده", "کرده‌ام",
        "نام", "اسم", "چیست", "چیه", "کی", "هستید");

    private static readonly HashSet<string> GreetingWords = Words("سلام", "درود", "الو");
    private static readonly HashSet<string> GreetingFillers = Words(
        "صبح", "صبحتون", "صبحتان", "ظهر", "ظهرتون", "ظهرتان", "عصر", "عصرتون", "عصرتان",
        "شب", "شبتون", "شبتان", "روز", "روزتون", "روزتان", "وقت", "وقتتون", "وقتتان",
        "عرض", "ادب", "احترام", "خدمت", "خدمتتون", "خدمتتان", "بخیر", "خیر", "خوش", "آمدید", "علیکم", "علیک",
        "خسته", "نباشید", "نباشین", "نباشی", "خدا", "قوت", "می", "کنم", "مجدد", "دوباره");

    private static readonly HashSet<string> ThanksWords = Words(
        "ممنون", "ممنونم", "ممنونیم", "متشکر", "متشکرم", "متشکریم", "مرسی", "سپاس",
        "سپاسگزار", "سپاسگزارم", "سپاسگزاریم", "تشکر", "دمت");
    private static readonly HashSet<string> ThanksContextWords = Words(
        "دست", "دستت", "دستتون", "دستتان", "درد", "نکنه", "نکند", "زحمت", "کشیدی", "کشیدین", "کشیدید",
        "لطف", "لطفت", "لطفتون", "لطفتان", "محبت", "کردی", "کردین", "کردید", "گرم", "فراوان",
        "بابت", "برای", "این", "همه", "را", "رو", "راهنمایی", "راهنماییتون", "راهنماییتان",
        "توضیح", "توضیحات", "توضیحتون", "توضیحتان", "دادید", "دادی", "پاسخ", "پاسختون", "پاسختان",
        "جواب", "کمک", "کمکتون", "کمکتان", "وقتی", "وقتتون", "وقتتان", "پیگیری", "پیگیریتون",
        "پیگیریتان", "خوبه", "خوبتون", "خوبتان", "عالی", "خدا", "خیرت", "خیرتون", "خیرتان", "بده", "یک", "دنیا",
        "گذاشتی", "گذاشتین", "گذاشتید",
        "زنده", "باشید");

    private static readonly HashSet<string> GoodbyeWords = Words(
        "خداحافظ", "بدرود", "خدانگهدار", "خدانگهدارتون", "خدانگهدارتان", "فعلا", "فعلاً");
    private static readonly HashSet<string> GoodbyeContextWords = Words(
        "خدا", "حافظ", "نگهدار", "موفق", "پیروز", "دیدار", "دیداری", "امید", "یا", "علی",
        "روز", "روزتون", "روزتان", "شب", "اوقات", "خوش", "خوبی", "داشته", "باشید", "تا", "بعد", "دیگر", "مجدد");

    private static readonly HashSet<string> WellbeingWords = Words(
        "حال", "حالت", "حالتون", "حالتان", "احوال", "احوالتون", "احوالتان", "اوضاع", "اوضاعتون", "اوضاعتان",
        "روزگار", "خوب", "خوبی", "خوبید", "خوبین", "خوبه", "خوبم", "چطور", "چطوره", "چطورید", "چطورین",
        "چطوری", "طوری", "خبر", "چخبر", "چخبرا", "سلامتی", "سلامت", "سلامتید", "امیدوارم", "باشه",
        "میگذره", "میگذرد", "هستی", "هستید", "هستین", "است");

    private static readonly HashSet<string> PositiveWellbeingWords = Words(
        "حال", "حالم", "خوب", "خوبه", "خوبم", "عالی", "عالیه", "ام", "عالی‌ام", "عالیام",
        "سرحالم", "الحمدلله", "شکر", "خدا", "امروز");
    private static readonly HashSet<string> LowWellbeingWords = Words(
        "بد", "بده", "نیستم", "نیست", "ندارم", "تعریفی", "امروز", "زیاد", "خوب", "حال", "حالم", "ممنون", "ممنونم");

    private static readonly HashSet<string> ApologyWords = Words(
        "ببخشید", "ببخشین", "معذرت", "پوزش", "عذر", "عذرخواهی", "شرمنده", "مزاحم");
    private static readonly HashSet<string> ApologyContextWords = Words(
        "وقت", "وقتتون", "وقتتان", "را", "رو", "گرفتم", "شدم", "می", "خواهم", "میخواهم", "میخوام",
        "کنم", "طلبم", "که", "شما");

    private static readonly HashSet<string> EmpathyWords = Words(
        "حیف", "افسوس", "متاسفم", "متأسفم", "متاسفانه", "متأسفانه");

    private static readonly HashSet<string> CourtesyWords = Words(
        "اختیار", "لطف", "محبت", "قابل", "ندارد", "نداره", "نداشت", "ارادتمندم", "قربان");

    private static readonly HashSet<string> PraiseWords = Words(
        "عالی", "عالیه", "خوب", "فوق", "العاده", "فوقالعاده", "فوق‌العاده", "مفید", "کامل", "راضی", "رضایت", "حل");

    private static readonly HashSet<string> AffirmationWords = Words("بله", "بلی", "آره", "اره", "آها", "آری");
    private static readonly HashSet<string> NegativeWords = Words("نه", "خیر", "نخیر");
    private static readonly HashSet<string> AcknowledgmentWords = Words(
        "باشه", "باشد", "حتما", "حتماً", "اوکی", "اوکیه", "فهمیدم", "متوجه", "شدم", "بسیار", "خوب", "خوبه",
        "درسته", "درست", "صحیح", "خواهش", "قابلی", "قبوله", "خب");

    private static readonly HashSet<string> CommonFillers = Words(
        "خیلی", "زیاد", "بسیار", "واقعا", "واقعاً", "از", "بر", "به", "با", "و", "که", "اما", "ولی", "را", "رو",
        "من", "منو", "مرا", "تو", "شما", "خودت", "خودتون", "خودتان", "تون", "تان", "هم", "همه", "یک", "امروز", "ازتون", "ازتان",
        "دارید", "دارین", "کنید", "کنین", "می", "کنم", "میکنم", "میخوام", "میخواهم", "هستم", "هستی",
        "هستید", "هستین", "هست", "است", "بود", "بودید", "باشید", "باشه", "نداشت", "نداره", "دیگه",
        "چه", "چطور", "چطوره", "چطورید", "چطورین", "تون", "تان", "زنده", "بفرما", "بفرمایید", "لطفا", "لطفاً");

    private static readonly HashSet<string> GreetingAllowedWords = MergeWords(
        GreetingWords, GreetingFillers, CommonFillers, ThanksWords);
    private static readonly HashSet<string> ThanksAllowedWords = MergeWords(
        ThanksWords, ThanksContextWords, CommonFillers, NegativeWords, AffirmationWords, PraiseWords);
    private static readonly HashSet<string> GoodbyeAllowedWords = MergeWords(
        GoodbyeWords, GoodbyeContextWords, CommonFillers, ThanksAllowedWords);
    private static readonly HashSet<string> WellbeingAllowedWords = MergeWords(
        WellbeingWords, PositiveWellbeingWords, ThanksWords, GreetingAllowedWords, CommonFillers);
    private static readonly HashSet<string> PositiveWellbeingAllowedWords = MergeWords(
        PositiveWellbeingWords, WellbeingWords, ThanksWords, CommonFillers, Words("شکر", "خدا", "الحمدلله", "سپاس"));
    private static readonly HashSet<string> LowWellbeingAllowedWords = MergeWords(
        LowWellbeingWords, ThanksWords, CommonFillers);
    private static readonly HashSet<string> ApologyAllowedWords = MergeWords(
        ApologyWords, ApologyContextWords, CommonFillers, GreetingWords, GreetingFillers);
    private static readonly HashSet<string> CourtesyAllowedWords = MergeWords(
        CourtesyWords, CommonFillers, Words("را", "رو", "شما"));
    private static readonly HashSet<string> PraiseAllowedWords = MergeWords(
        PraiseWords, ThanksWords, CommonFillers,
        Words("پاسخ", "پاسختون", "پاسختان", "راهنمایی", "توضیح", "توضیحات", "توضیح‌تون", "توضیحتون", "کردید", "دادید", "بود", "بودید", "مشکلم", "شد"));

    private static readonly HashSet<string> ContinueAllowedWords = MergeWords(
        CommonFillers, Words("ادامه", "بده", "بدید", "بدین", "دهید", "گوش", "میدم", "دم", "توضیحتون", "توضیحتان", "رو", "را", "خب"));
    private static readonly HashSet<string> AudioCheckAllowedWords = MergeWords(
        GreetingWords, CommonFillers,
        Words("صدا", "صدای", "صدام", "صداتون", "صدایتان", "میاد", "میاید", "می‌آید", "آید", "می", "شنوید", "میشنوید", "میشنوی", "دارید", "دارین", "واضح"));
    private static readonly HashSet<string> PresenceAllowedWords = MergeWords(
        GreetingWords, CommonFillers, Words("کسی", "اونجا", "آنجا", "پشت", "خط", "حضور"));
    private static readonly HashSet<string> RepairAllowedWords = MergeWords(
        CommonFillers,
        Words("متوجه", "نشد", "نشدم", "نفهمیدم", "نشنیدم", "دوباره", "تکرار", "بگید", "بگین", "میگید", "گید",
            "گفتید", "چی", "فرمودید", "یک", "بار", "دیگه", "صداتون", "صدایتان", "صدا", "قطع", "واضح", "نبود", "شد"));

    private static readonly HashSet<string> SimpleResponseAllowedWords = MergeWords(
        AffirmationWords, NegativeWords, AcknowledgmentWords, CommonFillers, ThanksWords);
    private static readonly HashSet<string> GeneralHelpAllowedWords = MergeWords(
        GreetingWords, GreetingFillers, ApologyWords, ApologyContextWords, CommonFillers,
        Words("کمک", "کمکم", "راهنمایی", "راهنماییم", "راهنمایی‌ام", "میشه", "میتونید", "میتونین", "میتونی",
            "می‌توانید", "تونید", "توانید", "یه", "سوال", "سؤال", "پرسش", "داشتم", "دارم", "درباره", "خدمات"));

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
        if (tokens.Count == 0 || tokens.Count > 20) return false;
        var isQuestion = text.Contains('?') || text.Contains('؟');

        var asksWellbeing = IsWellbeingInquiry(tokens);
        // در عبارت‌های ترکیبی، واژگان احوال‌پرسی باعث می‌شوند الگوی «سلام» یا «تشکر»
        // به‌تنهایی full-match نشود؛ وجود anchor را فقط پس از اثبات اجتماعی‌بودن کل عبارت می‌پذیریم.
        var hasGreeting = IsGreeting(tokens) ||
                          (asksWellbeing && tokens.Any(GreetingWords.Contains));
        var hasThanks = IsThanks(tokens) ||
                        (asksWellbeing && tokens.Any(ThanksWords.Contains));

        if (IsAudioConnectionCheck(tokens))
        {
            response = "بله، صدایتان را واضح می‌شنوم. بفرمایید.";
            return true;
        }

        if (IsPresenceCheck(tokens))
        {
            response = "بله، در خدمتتان هستم. بفرمایید.";
            return true;
        }

        if (IsRepairRequest(tokens))
        {
            response = "حتماً، ببخشید. لطفاً بفرمایید کدام بخش را دوباره تکرار کنم.";
            return true;
        }

        if (IsContinueRequest(tokens))
        {
            response = "حتماً؛ لطفاً بفرمایید کدام بخش را ادامه بدهم.";
            return true;
        }

        if (IsGeneralHelpRequest(tokens))
        {
            response = "حتماً، با کمال میل. لطفاً سؤال‌تان را بفرمایید.";
            return true;
        }

        if (IsLowWellbeingReply(tokens))
        {
            response = "متأسفم که امروز حالتان کاملاً خوب نیست. امیدوارم زودتر بهتر شوید؛ اگر کمکی از من برمی‌آید، بفرمایید.";
            return true;
        }

        if (IsNeutralWellbeingReply(tokens))
        {
            response = "خوشحالم که حالتان بد نیست؛ امیدوارم هر لحظه بهتر باشید. بفرمایید، چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (IsPositiveWellbeingReply(tokens))
        {
            response = asksWellbeing
                ? "خوشحالم که حالتان خوب است. ممنون، من هم خوبم؛ بفرمایید چطور می‌توانم کمکتان کنم."
                : "خوشحالم که حالتان خوب است. بفرمایید، چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (asksWellbeing)
        {
            if (hasGreeting)
                response = "سلام، ممنون از احوال‌پرسی شما؛ خوبم و با کمال میل آماده‌ام کمکتان کنم.";
            else if (hasThanks)
                response = "خواهش می‌کنم. ممنون از احوال‌پرسی شما؛ خوبم و با کمال میل آماده‌ام کمکتان کنم.";
            else
                response = "ممنون، خوبم و از احوال‌پرسی شما سپاسگزارم؛ با کمال میل آماده‌ام کمکتان کنم.";
            return true;
        }

        if (IsGoodbye(tokens))
        {
            response = hasThanks || tokens.Any(ThanksWords.Contains)
                ? "خواهش می‌کنم. خدانگهدار، روز خوبی داشته باشید."
                : "خدانگهدار، روز خوبی داشته باشید.";
            return true;
        }

        if (hasThanks)
        {
            response = "خواهش می‌کنم. خوشحالم که توانستم کمک کنم؛ اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        if (IsCourtesy(tokens))
        {
            response = "از لطف شما ممنونم. بفرمایید، با کمال میل در خدمتتان هستم.";
            return true;
        }

        if (IsPraise(tokens, isQuestion))
        {
            response = "از لطف شما ممنونم؛ خوشحالم که پاسخ برایتان مفید بود. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, ApologyWords, ApologyAllowedWords))
        {
            response = tokens.Any(GreetingWords.Contains)
                ? "سلام، خواهش می‌کنم؛ مزاحمتی نیست. بفرمایید، چطور می‌توانم کمکتان کنم؟"
                : "خواهش می‌کنم، بفرمایید. اشکالی ندارد؛ چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, EmpathyWords, MergeWords(EmpathyWords, CommonFillers)))
        {
            response = "متوجه‌ام. اگر مایل باشید، بفرمایید چطور می‌توانم کمکتان کنم.";
            return true;
        }

        if (hasGreeting)
        {
            if (tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase) && tokens.Contains("قوت", StringComparer.OrdinalIgnoreCase))
                response = "سلامت باشید! خوش آمدید. چطور می‌توانم کمکتان کنم؟";
            else
                response = "سلام! خوش آمدید. با کمال میل آماده‌ام کمکتان کنم؛ بفرمایید.";
            return true;
        }

        if (IsReciprocalCourtesy(tokens))
        {
            response = "از لطف شما ممنونم. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        if (ContainsOnlyIntent(tokens, AffirmationWords, SimpleResponseAllowedWords))
        {
            response = "بله، بفرمایید. چطور می‌توانم کمکتان کنم؟";
            return true;
        }

        if (ContainsOnlyIntent(tokens, NegativeWords, SimpleResponseAllowedWords))
        {
            response = "بسیار خوب. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        if (!isQuestion && ContainsOnlyIntent(tokens, AcknowledgmentWords, SimpleResponseAllowedWords))
        {
            response = "حتماً. اگر سؤال دیگری دارید، بفرمایید.";
            return true;
        }

        return false;
    }

    public static bool HasMeaningfulInput(string text) => Tokenize(text).Count > 0;

    private static bool IsGreeting(IReadOnlyCollection<string> tokens)
    {
        var explicitGreeting = ContainsOnlyIntent(tokens, GreetingWords, GreetingAllowedWords);
        var timeGreeting = tokens.Any(token => token is
                "صبح" or "صبحتون" or "صبحتان" or "ظهر" or "ظهرتون" or "ظهرتان" or
                "عصر" or "عصرتون" or "عصرتان" or "شب" or "شبتون" or "شبتان" or
                "روز" or "روزتون" or "روزتان" or "وقت" or "وقتتون" or "وقتتان") &&
            (tokens.Contains("بخیر", StringComparer.OrdinalIgnoreCase) ||
             (tokens.Contains("به", StringComparer.OrdinalIgnoreCase) && tokens.Contains("خیر", StringComparer.OrdinalIgnoreCase))) &&
            tokens.All(GreetingAllowedWords.Contains);
        var tiredGreeting = tokens.Contains("خسته", StringComparer.OrdinalIgnoreCase) &&
                            tokens.Any(token => token is "نباشید" or "نباشین" or "نباشی") &&
                            tokens.All(GreetingAllowedWords.Contains);
        var formalGreeting = tokens.Contains("عرض", StringComparer.OrdinalIgnoreCase) &&
                             tokens.Any(token => token is "ادب" or "احترام" or "سلام") &&
                             tokens.All(GreetingAllowedWords.Contains);
        var encouragementGreeting = tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase) &&
                                    tokens.Contains("قوت", StringComparer.OrdinalIgnoreCase) &&
                                    tokens.All(GreetingAllowedWords.Contains);
        return explicitGreeting || timeGreeting || tiredGreeting || formalGreeting || encouragementGreeting;
    }

    private static bool IsWellbeingInquiry(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(WellbeingAllowedWords.Contains)) return false;

        var direct = tokens.Any(token => token is
            "خوبی" or "خوبید" or "خوبین" or "چطوری" or "چطورید" or "چطورین" or
            "سلامتید" or "چخبر" or "چخبرا");
        var refersToCaller = tokens.Contains("من", StringComparer.OrdinalIgnoreCase) &&
                             !tokens.Contains("شما", StringComparer.OrdinalIgnoreCase);
        var asksAboutState = !refersToCaller && tokens.Any(token => token is
                "حال" or "حالت" or "حالتون" or "حالتان" or "احوال" or "احوالتون" or "احوالتان" or
                "اوضاع" or "اوضاعتون" or "اوضاعتان" or "روزگار") &&
            tokens.Any(token => token is "چطور" or "چطوره" or "چطورید" or "چطورین" or "خوب" or "خوبه");
        var asksGood = tokens.Any(token => token is "خوب" or "سلامت") &&
                       tokens.Any(token => token is "هستی" or "هستید" or "هستین");
        var asksNews = !refersToCaller && tokens.Contains("خبر", StringComparer.OrdinalIgnoreCase) &&
                       tokens.Contains("چه", StringComparer.OrdinalIgnoreCase);
        var reciprocal = tokens.Any(token => token is "شما" or "خودتون" or "خودتان") &&
                         tokens.Any(token => token is "چطور" or "چطوره" or "چطورید" or "چطورین");
        var kindWish = tokens.Contains("امیدوارم", StringComparer.OrdinalIgnoreCase) &&
                       tokens.Any(token => token is "حال" or "حالتون" or "حالتان") &&
                       tokens.Any(token => token is "خوب" or "خوبه");
        var separatedHow = !refersToCaller && tokens.Contains("چه", StringComparer.OrdinalIgnoreCase) &&
                           tokens.Contains("طوری", StringComparer.OrdinalIgnoreCase);
        return direct || asksAboutState || asksGood || asksNews || reciprocal || kindWish || separatedHow;
    }

    private static bool IsPositiveWellbeingReply(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(PositiveWellbeingAllowedWords.Contains)) return false;
        return tokens.Any(token => token is "خوبم" or "عالی‌ام" or "عالیام" or "سرحالم") ||
               (tokens.Contains("عالی", StringComparer.OrdinalIgnoreCase) &&
                tokens.Any(token => token is "ام" or "هستم")) ||
               (tokens.Any(token => token is "حال" or "حالم") &&
                tokens.Any(token => token is "خوب" or "خوبه" or "عالی" or "عالیه")) ||
               (tokens.Contains("خوب", StringComparer.OrdinalIgnoreCase) &&
                tokens.Contains("هستم", StringComparer.OrdinalIgnoreCase)) ||
               tokens.Contains("الحمدلله", StringComparer.OrdinalIgnoreCase) ||
               (tokens.Contains("شکر", StringComparer.OrdinalIgnoreCase) && tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsLowWellbeingReply(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(LowWellbeingAllowedWords.Contains)) return false;
        return (tokens.Contains("تعریفی", StringComparer.OrdinalIgnoreCase) && tokens.Contains("ندارم", StringComparer.OrdinalIgnoreCase)) ||
               (tokens.Contains("خوب", StringComparer.OrdinalIgnoreCase) && tokens.Any(token => token is "نیستم" or "نیست")) ||
               (tokens.Any(token => token is "حال" or "حالم") && tokens.Contains("بده", StringComparer.OrdinalIgnoreCase)) ||
               (tokens.Any(token => token is "حال" or "حالم") && tokens.Contains("ندارم", StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsNeutralWellbeingReply(IReadOnlyCollection<string> tokens)
        => tokens.All(LowWellbeingAllowedWords.Contains) &&
           tokens.Contains("بد", StringComparer.OrdinalIgnoreCase) &&
           tokens.Contains("نیستم", StringComparer.OrdinalIgnoreCase);

    private static bool IsThanks(IReadOnlyCollection<string> tokens)
    {
        var simple = ContainsOnlyIntent(tokens, ThanksWords, ThanksAllowedWords);
        var polite = tokens.Any(token => token is "لطف" or "محبت") &&
                     tokens.Any(token => token is "کردی" or "کردین" or "کردید") &&
                     tokens.All(ThanksAllowedWords.Contains);
        var handPain = tokens.Any(token => token is "دست" or "دستت" or "دستتون" or "دستتان") &&
                       tokens.Contains("درد", StringComparer.OrdinalIgnoreCase) &&
                       tokens.Any(token => token is "نکنه" or "نکند") &&
                       tokens.All(ThanksAllowedWords.Contains);
        var effort = tokens.Contains("زحمت", StringComparer.OrdinalIgnoreCase) &&
                     tokens.Any(token => token is "کشیدی" or "کشیدین" or "کشیدید") &&
                     tokens.All(ThanksAllowedWords.Contains);
        var divine = tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase) &&
                      tokens.Any(token => token is "خیرت" or "خیرتون" or "خیرتان") &&
                     tokens.Contains("بده", StringComparer.OrdinalIgnoreCase) &&
                     tokens.All(ThanksAllowedWords.Contains);
        var kindWish = tokens.Contains("زنده", StringComparer.OrdinalIgnoreCase) &&
                       tokens.Contains("باشید", StringComparer.OrdinalIgnoreCase) &&
                       tokens.All(ThanksAllowedWords.Contains);
        return simple || polite || handPain || effort || divine || kindWish;
    }

    private static bool IsGoodbye(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(GoodbyeAllowedWords.Contains)) return false;
        return tokens.Any(GoodbyeWords.Contains) ||
               (tokens.Contains("خدا", StringComparer.OrdinalIgnoreCase) && tokens.Any(token => token is "حافظ" or "نگهدار")) ||
               (tokens.Any(token => token is "موفق" or "پیروز") && tokens.Contains("باشید", StringComparer.OrdinalIgnoreCase)) ||
               (tokens.Contains("امید", StringComparer.OrdinalIgnoreCase) && tokens.Any(token => token is "دیدار" or "دیداری")) ||
               (tokens.Contains("یا", StringComparer.OrdinalIgnoreCase) && tokens.Contains("علی", StringComparer.OrdinalIgnoreCase)) ||
               (tokens.Any(token => token is "روز" or "روزتون" or "روزتان" or "شب" or "اوقات") &&
                (tokens.Contains("خوش", StringComparer.OrdinalIgnoreCase) ||
                 (tokens.Contains("خوبی", StringComparer.OrdinalIgnoreCase) &&
                  tokens.Contains("داشته", StringComparer.OrdinalIgnoreCase) &&
                  tokens.Contains("باشید", StringComparer.OrdinalIgnoreCase)))) ||
               (tokens.Contains("تا", StringComparer.OrdinalIgnoreCase) && tokens.Any(token => token is "دیدار" or "دیداری"));
    }

    private static bool IsCourtesy(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(CourtesyAllowedWords.Contains)) return false;
        return (tokens.Contains("اختیار", StringComparer.OrdinalIgnoreCase) && tokens.Contains("دارید", StringComparer.OrdinalIgnoreCase)) ||
               (tokens.Any(token => token is "لطف" or "محبت") && tokens.Contains("دارید", StringComparer.OrdinalIgnoreCase)) ||
               (tokens.Contains("قابل", StringComparer.OrdinalIgnoreCase) && tokens.Contains("شما", StringComparer.OrdinalIgnoreCase) &&
                tokens.Any(token => token is "ندارد" or "نداره" or "نداشت")) ||
               tokens.Contains("ارادتمندم", StringComparer.OrdinalIgnoreCase) ||
               (tokens.Contains("قربان", StringComparer.OrdinalIgnoreCase) && tokens.Contains("شما", StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsPraise(IReadOnlyCollection<string> tokens, bool isQuestion)
    {
        if (isQuestion || !tokens.All(PraiseAllowedWords.Contains)) return false;
        var hasPraise = tokens.Any(token => token is not "فوق" and not "العاده" && PraiseWords.Contains(token)) ||
                        (tokens.Contains("فوق", StringComparer.OrdinalIgnoreCase) &&
                         tokens.Contains("العاده", StringComparer.OrdinalIgnoreCase));
        return hasPraise &&
               (tokens.Any(token => token is "بود" or "بودید" or "عالیه" or "کردید" or "دادید" or "شد") || tokens.Count == 1);
    }

    private static bool IsGeneralHelpRequest(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(GeneralHelpAllowedWords.Contains)) return false;
        var asksForHelp = tokens.Any(token => token is "کمک" or "کمکم" or "راهنمایی" or "راهنماییم" or "راهنمایی‌ام") &&
                          tokens.Any(token => token is "کنید" or "کنین" or "میشه" or "میتونید" or "میتونین" or "میتونی" or "می‌توانید" or "تونید" or "توانید");
        var announcesQuestion = tokens.Any(token => token is "سوال" or "سؤال" or "پرسش") &&
                                tokens.Any(token => token is "دارم" or "داشتم");
        return asksForHelp || announcesQuestion;
    }

    private static bool IsContinueRequest(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(ContinueAllowedWords.Contains)) return false;
        var explicitContinue = tokens.Contains("ادامه", StringComparer.OrdinalIgnoreCase) &&
                               tokens.Any(token => token is "بده" or "بدید" or "بدین" or "دهید" or "بفرمایید");
        var listening = tokens.Contains("گوش", StringComparer.OrdinalIgnoreCase) &&
                        (tokens.Contains("میدم", StringComparer.OrdinalIgnoreCase) ||
                         (tokens.Contains("می", StringComparer.OrdinalIgnoreCase) && tokens.Contains("دم", StringComparer.OrdinalIgnoreCase))) &&
                        tokens.Contains("بفرمایید", StringComparer.OrdinalIgnoreCase);
        return explicitContinue || listening;
    }

    private static bool IsAudioConnectionCheck(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(AudioCheckAllowedWords.Contains)) return false;
        var hasAudio = tokens.Any(token => token is "صدا" or "صدای" or "صدام" or "صداتون" or "صدایتان");
        var hasHearing = tokens.Any(token => token is "میشنوید" or "میشنوی" or "شنوید") ||
                         (tokens.Contains("می", StringComparer.OrdinalIgnoreCase) && tokens.Contains("شنوید", StringComparer.OrdinalIgnoreCase));
        var arrives = tokens.Any(token => token is "میاد" or "میاید" or "آید") ||
                      (tokens.Contains("می", StringComparer.OrdinalIgnoreCase) && tokens.Contains("آید", StringComparer.OrdinalIgnoreCase));
        return (hasAudio && (hasHearing || arrives || tokens.Any(token => token is "دارید" or "دارین"))) ||
               (tokens.Contains("منو", StringComparer.OrdinalIgnoreCase) && hasHearing);
    }

    private static bool IsPresenceCheck(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(PresenceAllowedWords.Contains)) return false;
        var available = tokens.Any(token => token is "هست" or "هستید" or "هستین");
        return available && (tokens.Contains("کسی", StringComparer.OrdinalIgnoreCase) ||
                             (tokens.Contains("پشت", StringComparer.OrdinalIgnoreCase) && tokens.Contains("خط", StringComparer.OrdinalIgnoreCase)));
    }

    private static bool IsRepairRequest(IReadOnlyCollection<string> tokens)
    {
        if (!tokens.All(RepairAllowedWords.Contains)) return false;
        return (tokens.Contains("متوجه", StringComparer.OrdinalIgnoreCase) && tokens.Any(token => token is "نشد" or "نشدم")) ||
               tokens.Any(token => token is "نفهمیدم" or "نشنیدم") ||
               ((tokens.Contains("دوباره", StringComparer.OrdinalIgnoreCase) ||
                 (tokens.Contains("یک", StringComparer.OrdinalIgnoreCase) &&
                  tokens.Contains("بار", StringComparer.OrdinalIgnoreCase) &&
                  tokens.Contains("دیگه", StringComparer.OrdinalIgnoreCase))) &&
                tokens.Any(token => token is "بگید" or "بگین" or "میگید" or "گید" or "فرمودید")) ||
               (tokens.Contains("تکرار", StringComparer.OrdinalIgnoreCase) && tokens.Any(token => token is "کنید" or "کنین")) ||
               (tokens.Any(token => token is "صدا" or "صداتون" or "صدایتان") &&
                tokens.Contains("قطع", StringComparer.OrdinalIgnoreCase) && tokens.Contains("شد", StringComparer.OrdinalIgnoreCase)) ||
               (tokens.Contains("چی", StringComparer.OrdinalIgnoreCase) && tokens.Any(token => token is "گفتید" or "فرمودید"));
    }

    private static bool IsReciprocalCourtesy(IReadOnlyCollection<string> tokens)
        => tokens.All(SimpleResponseAllowedWords.Contains) &&
           ((tokens.Contains("خواهش", StringComparer.OrdinalIgnoreCase) &&
             tokens.Any(token => token is "کنم" or "میکنم")) ||
             (tokens.Contains("قابلی", StringComparer.OrdinalIgnoreCase) &&
              tokens.Any(token => token is "نداشت" or "نداره" or "ندارد")));

    private static bool ContainsOnlyIntent(
        IReadOnlyCollection<string> tokens,
        HashSet<string> intentWords,
        HashSet<string> allowedFillers)
        => tokens.Any(intentWords.Contains) && tokens.All(allowedFillers.Contains);

    private static HashSet<string> Words(params string[] words)
        => words.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> MergeWords(params IEnumerable<string>[] groups)
        => groups.SelectMany(group => group).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static List<string> Tokenize(string text)
    {
        var normalized = NormalizeText(text);
        return Regex.Split(normalized, @"[^\p{L}\p{N}]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    private static string NormalizeText(string? text)
    {
        var normalized = (text ?? "")
            .Normalize(NormalizationForm.FormKC)
            .Replace('ي', 'ی')
            .Replace('ى', 'ی')
            .Replace('ك', 'ک')
            .Replace("‌", " ")
            .ToLowerInvariant();
        return Regex.Replace(normalized, "[\\u064B-\\u065F\\u0670]", "");
    }
}
