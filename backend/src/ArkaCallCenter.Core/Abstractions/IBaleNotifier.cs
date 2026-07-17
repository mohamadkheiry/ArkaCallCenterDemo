using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Abstractions;

/// <summary>
/// اعلامِ کاربرانِ جدیدِ دمو در کانالِ بله (حداکثر سه پیام به‌ازای هر کاربر:
/// شماره → نام → داخلی).
/// </summary>
public interface IBaleNotifier
{
    /// <summary>
    /// ارسالِ پیامِ یک مرحله به‌صورت «آتش‌کن‌و‌فراموش‌کن»؛ هرگز جریانِ کاربر را کند یا خراب نمی‌کند.
    /// هر مرحله برای هر شماره حداکثر یک پیامِ موفق دارد.
    /// </summary>
    void Enqueue(LeadStage stage, string phoneNumber);

    /// <summary>ارسالِ پیامِ آزمایشی به کانال (برای دکمه‌ی «تست» در پنل سوپرادمین).</summary>
    Task<(bool ok, string? error)> SendTestAsync(CancellationToken ct = default);
}
