namespace ArkaCallCenter.Core.Constants;

/// <summary>پیام‌های استاندارد مکالمه که در API، seed و worker باید یکسان باشند.</summary>
public static class ConversationMessages
{
    public const string UnknownKnowledge =
        "پاسخ این سؤال را نمی‌دانم. بهتر است سؤال خود را از کارشناسان یا اپراتورها بپرسید.";

    public static readonly string[] LegacyUnknownKnowledgeMessages =
    {
        "پاسخ این سوال در پایگاه دانش من موجود نیست.",
        "پاسخ این سؤال در پایگاه دانش من موجود نیست.",
    };
}
