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
    public const string SmsIrLineNumber = "smsir.lineNumber";

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
}
