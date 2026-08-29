using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// snapshot آخرین نتیجهٔ ارسال هر «مرحله» از لید یک شماره به CRM فروش.
/// این رکورد برای عیب‌یابی است و مانع ارسال دوبارهٔ همان شماره/مرحله نمی‌شود.
/// </summary>
public class CrmLeadSubmission : BaseEntity
{
    /// <summary>شماره‌ی موبایلِ نرمال‌شده (کلیدِ تشخیصِ کاربر؛ در مرحله‌ی اول هنوز User وجود ندارد).</summary>
    public string PhoneNumber { get; set; } = default!;

    public LeadStage Stage { get; set; }

    /// <summary>آیا CRM در آخرین تلاش پاسخ موفق (success=true) داد؟</summary>
    public bool Success { get; set; }

    /// <summary>پیام/کدِ بازگشتی از CRM برای عیب‌یابی.</summary>
    public string? ResponseMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
