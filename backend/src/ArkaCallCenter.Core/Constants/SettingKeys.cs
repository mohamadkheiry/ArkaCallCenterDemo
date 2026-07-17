namespace ArkaCallCenter.Core.Constants;

/// <summary>کلیدهای استاندارد جدول AppSetting (تنظیمات سوپرادمین).</summary>
public static class SettingKeys
{
    // OpenAI
    public const string OpenAiBaseUrl = "openai.baseUrl";
    public const string OpenAiApiKey = "openai.apiKey";                 // secret
    public const string OpenAiEmbeddingModel = "openai.embeddingModel";
    public const string OpenAiRealtimeModel = "openai.realtimeModel";
    public const string OpenAiTtsModel = "openai.ttsModel";
    public const string OpenAiChatModel = "openai.chatModel";           // برای moderation و ابزارهای متنی

    // SMS.ir
    public const string SmsIrApiKey = "smsir.apiKey";                   // secret
    public const string SmsIrLineNumber = "smsir.lineNumber";           // برای پیامک‌های bulk رویدادها
    public const string SmsIrVerifyTemplateId = "smsir.verifyTemplateId"; // قالب ارسال کد تأیید (/send/verify)

    // CRM فروش (ExternalEndpoint/InsertContactUs) — ثبتِ لیدِ کاربرانِ دمو
    public const string CrmEnabled = "crm.enabled";                     // "true"/"false"
    public const string CrmBaseUrl = "crm.baseUrl";                     // مثلاً https://api.arkadp.com
    public const string CrmApiKey = "crm.apiKey";                       // secret (هدر X-Api-Key)
    public const string CrmEmailDomain = "crm.emailDomain";             // دامنه‌ی ایمیلِ ساختگی از روی شماره

    // کانال بله (Bale) — اطلاع‌رسانیِ کاربرانِ جدیدِ دمو در کانال
    public const string BaleEnabled = "bale.enabled";                   // "true"/"false"
    public const string BaleBaseUrl = "bale.baseUrl";                   // پیش‌فرض https://tapi.bale.ai
    public const string BaleBotToken = "bale.botToken";                 // secret
    public const string BaleChannelId = "bale.channelId";               // آی‌دیِ پابلیکِ کانال، مثلاً @my_channel

    // Voice / defaults
    public const string DefaultVoiceName = "voice.default";
    public const string VoiceSampleText = "voice.sampleText"; // متن نمونه‌صدای گوینده‌ها

    // Limits
    public const string DefaultCallMinuteLimit = "limits.defaultCallMinutes";
    public const string CallLimitWarningPercent = "limits.warningPercent";

    // RAG
    public const string RagSimilarityThreshold = "rag.similarityThreshold";
    public const string RagTopK = "rag.topK";

    // Fallback ("پاسخ این سوال در پایگاه دانش من موجود نیست")
    public const string FallbackMessageText = "fallback.text";
    public const string FallbackMessageVoice = "fallback.voice";
    public const string FallbackAudioPath = "fallback.audioPath";

    // شماره‌ی پذیرش شرکت که کاربران باید ابتدا با آن تماس بگیرند، سپس داخلی خود را شماره‌گیری کنند.
    public const string ReceptionNumber = "reception.number";

    // پیام پذیرش اصلی شرکت (IVR): پخش می‌شود و سپس منتظر دریافت داخلی می‌ماند.
    public const string MainGreetingText = "main.greetingText";
    public const string MainGreetingVoice = "main.greetingVoice";
    public const string MainGreetingAudioPath = "main.greetingAudioPath"; // فایل WAV روی ایزابل
    public const string MainGreetingAsteriskSound = "main.asteriskSound";  // نام sound برای dialplan

    // موسیقی انتظار (حین «فکر کردن» هوش مصنوعی)
    public const string HoldMusicEnabled = "hold.enabled";
    public const string HoldMusicPath = "hold.slinPath"; // SLIN 8kHz raw روی worker

    // ویدیوی آموزشی (آپلود توسط سوپرادمین، نمایش به کاربران)
    public const string TutorialVideoPath = "tutorial.videoPath";

    // لوگوی سامانه (آپلود توسط سوپرادمین)
    public const string SystemLogoPath = "branding.logoPath";

    // ضبط مکالمه‌ها
    public const string CallRecordingEnabled = "call.recordingEnabled";
}
