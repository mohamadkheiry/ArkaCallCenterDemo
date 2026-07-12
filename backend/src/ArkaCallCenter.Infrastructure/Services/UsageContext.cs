using ArkaCallCenter.Core.Abstractions;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>پیاده‌سازی scoped نگه‌دارنده‌ی هویت کاربر جاری.</summary>
public class UsageContext : IUsageContext
{
    public int? UserId { get; set; }
    public string? PhoneNumber { get; set; }
}
