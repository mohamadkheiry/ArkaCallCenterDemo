using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Abstractions;

/// <summary>ارسال پیامک از طریق SMS.ir.</summary>
public interface ISmsSender
{
    /// <summary>ارسال پیامک متنی خام (bulk با شماره خط) — برای پیامک رویدادها.</summary>
    Task<bool> SendAsync(string phoneNumber, string text, CancellationToken ct = default);

    /// <summary>
    /// ارسال کد تأیید از طریق قالب SMS.ir (endpoint /send/verify، پارامتر CODE).
    /// برای کدهای ورود و تغییر شماره استفاده می‌شود.
    /// </summary>
    Task<bool> SendVerifyCodeAsync(string phoneNumber, string code, CancellationToken ct = default);
}

/// <summary>
/// موتور رویداد→پیامک: بر اساس قالب و گیرندگانِ تنظیم‌شده‌ی هر رویداد،
/// پیامک(های) مربوطه را ارسال می‌کند.
/// </summary>
public interface ISmsEventDispatcher
{
    Task DispatchAsync(SmsEventType eventType, IDictionary<string, string> variables,
        string? relatedUserPhone = null, CancellationToken ct = default);
}
