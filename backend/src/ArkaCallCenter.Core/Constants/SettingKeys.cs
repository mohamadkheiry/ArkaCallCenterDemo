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

    // SMS.ir
    public const string SmsIrApiKey = "smsir.apiKey";                   // secret
    public const string SmsIrLineNumber = "smsir.lineNumber";

    // Voice / defaults
    public const string DefaultVoiceName = "voice.default";

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
}
