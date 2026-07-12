using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

public class User : BaseEntity
{
    public string PhoneNumber { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? BrandName { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;

    /// <summary>true وقتی نام/برند تکمیل شده باشد (پروفایل کامل).</summary>
    public bool ProfileCompleted { get; set; }

    /// <summary>گوینده‌ی انتخابی کاربر برای صدای تلفن هوشمند (از VoiceOption).</summary>
    public string? VoiceName { get; set; }

    /// <summary>مسیر فایل تصویر پروفایل کاربر.</summary>
    public string? AvatarPath { get; set; }

    /// <summary>override محدودیت مکالمه بر حسب دقیقه؛ null یعنی استفاده از مقدار پیش‌فرض سراسری.</summary>
    public int? CallMinuteLimit { get; set; }

    /// <summary>مجموع دقایق مصرف‌شده (برای اعمال محدودیت).</summary>
    public int UsedMinutes { get; set; }

    /// <summary>
    /// دمو: یک پروفایل آزمایشی که سوپرادمین می‌سازد و مدیریت می‌کند (داخلی ۱–۹۹۹).
    /// دموها لاگین ندارند و PhoneNumber آن‌ها مقدار مصنوعی (demo{ext}) است.
    /// </summary>
    public bool IsDemo { get; set; }
    public string? DemoLabel { get; set; }

    public SmartPhone? SmartPhone { get; set; }
    public KnowledgeBase? KnowledgeBase { get; set; }
}
