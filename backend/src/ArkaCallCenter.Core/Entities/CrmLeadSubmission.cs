using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// ثبتِ اینکه کدام «مرحله»ی لید برای یک شماره به CRM فروش ارسال شده است.
/// هدف: هر مرحله برای هر شماره فقط یک‌بار ارسال شود (جلوگیری از لیدِ تکراری).
/// </summary>
public class CrmLeadSubmission : BaseEntity
{
    /// <summary>شماره‌ی موبایلِ نرمال‌شده (کلیدِ تشخیصِ کاربر؛ در مرحله‌ی اول هنوز User وجود ندارد).</summary>
    public string PhoneNumber { get; set; } = default!;

    public CrmLeadStage Stage { get; set; }

    /// <summary>آیا CRM پاسخِ موفق داد (success=true)؟ فقط ارسالِ موفق «انجام‌شده» تلقی می‌شود.</summary>
    public bool Success { get; set; }

    /// <summary>پیام/کدِ بازگشتی از CRM برای عیب‌یابی.</summary>
    public string? ResponseMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
