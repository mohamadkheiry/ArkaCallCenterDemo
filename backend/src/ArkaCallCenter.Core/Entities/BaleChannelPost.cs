using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// ثبتِ اینکه کدام «مرحله» برای یک شماره در کانالِ بله اعلام شده است.
/// هدف: حداکثر سه پیام به‌ازای هر کاربر (هر مرحله فقط یک‌بار).
/// </summary>
public class BaleChannelPost : BaseEntity
{
    /// <summary>شماره‌ی موبایلِ نرمال‌شده (در مرحله‌ی اول هنوز User وجود ندارد).</summary>
    public string PhoneNumber { get; set; } = default!;

    public LeadStage Stage { get; set; }

    /// <summary>آیا بله پاسخِ موفق داد (ok=true)؟ فقط ارسالِ موفق «انجام‌شده» تلقی می‌شود.</summary>
    public bool Success { get; set; }

    /// <summary>توضیحِ خطا/نتیجه برای عیب‌یابی.</summary>
    public string? ResponseMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
