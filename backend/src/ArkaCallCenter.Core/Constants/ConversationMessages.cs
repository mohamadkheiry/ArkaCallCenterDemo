namespace ArkaCallCenter.Core.Constants;

/// <summary>پیام‌های استاندارد مکالمه که در API، seed و worker باید یکسان باشند.</summary>
public static class ConversationMessages
{
    public const string UnknownKnowledge =
        "پاسخ این سؤال را نمی‌دانم. بهتر است سؤال خود را از کارشناسان یا اپراتورها بپرسید.";

    public const string RetrievalUnavailable =
        "در حال حاضر امکان بررسی اطلاعات فراهم نیست. لطفاً کمی بعد دوباره تلاش کنید یا با کارشناس و اپراتور صحبت کنید.";

    public static readonly string[] LegacyUnknownKnowledgeMessages =
    {
        "پاسخ این سوال در پایگاه دانش من موجود نیست.",
        "پاسخ این سؤال در پایگاه دانش من موجود نیست.",
    };

    public static string EnsureOperatorEscalation(string? configuredMessage)
    {
        var message = string.IsNullOrWhiteSpace(configuredMessage)
            ? UnknownKnowledge
            : configuredMessage.Trim();
        if (LegacyUnknownKnowledgeMessages.Contains(message, StringComparer.Ordinal))
            return UnknownKnowledge;
        if (message.Contains("اپراتور", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("کارشناس", StringComparison.OrdinalIgnoreCase))
            return message;

        return $"{message} برای راهنمایی دقیق‌تر، لطفاً با کارشناس یا اپراتور صحبت کنید.";
    }

    public static string CreateOutOfDomain(string? brandName, string? scopeDescription)
    {
        var brand = string.IsNullOrWhiteSpace(brandName) ? null : brandName.Trim();
        var scope = string.IsNullOrWhiteSpace(scopeDescription) ? null : scopeDescription.Trim();

        if (brand is not null && scope is not null)
            return $"من دستیار تلفنی «{brand}» هستم و می‌توانم دربارهٔ {scope} راهنمایی‌تان کنم. لطفاً سؤال‌تان را در همین زمینه بپرسید.";
        if (scope is not null)
            return $"من می‌توانم دربارهٔ {scope} راهنمایی‌تان کنم. لطفاً سؤال‌تان را در همین زمینه بپرسید.";
        if (brand is not null)
            return $"من دستیار تلفنی «{brand}» هستم و می‌توانم دربارهٔ محصولات، خدمات و اطلاعات این مجموعه راهنمایی‌تان کنم. لطفاً سؤال‌تان را در همین زمینه بپرسید.";

        return "من می‌توانم دربارهٔ محصولات، خدمات و اطلاعات این مجموعه راهنمایی‌تان کنم. لطفاً سؤال‌تان را در همین زمینه بپرسید.";
    }
}
