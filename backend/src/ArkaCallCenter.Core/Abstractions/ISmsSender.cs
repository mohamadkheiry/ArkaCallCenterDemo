using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Abstractions;

/// <summary>ارسال پیامک خام. پیاده‌سازی واقعی SMS.ir در فاز ۴.</summary>
public interface ISmsSender
{
    Task<bool> SendAsync(string phoneNumber, string text, CancellationToken ct = default);
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
