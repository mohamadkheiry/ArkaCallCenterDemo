using ArkaCallCenter.Core.Entities;

namespace ArkaCallCenter.Core.Abstractions;

/// <summary>تخصیص داخلی آزاد در بازه‌ی ۱۰۰۰–۹۹۹۹ (یکتا، بدون تکرار).</summary>
public interface IExtensionAllocator
{
    Task<int> AllocateAsync(CancellationToken ct = default);
}

public record ProvisionResult(bool Success, string? Error);

/// <summary>ساخت/حذف داخلی روی سرور ایزابل (Asterisk).</summary>
public interface IAsteriskProvisioningService
{
    Task<ProvisionResult> ProvisionExtensionAsync(int extension, string secret, CancellationToken ct = default);
    Task RemoveExtensionAsync(int extension, CancellationToken ct = default);
}

public record SmartPhoneResult(bool Ok, string? Error, SmartPhone? SmartPhone);

/// <summary>ارکستراسیون ساخت تلفن هوشمند: پیش‌نیازها، تخصیص داخلی، provisioning، وویس خوش‌آمد، پیامک.</summary>
public interface ISmartPhoneService
{
    Task<SmartPhone?> GetAsync(int userId, CancellationToken ct = default);
    Task<SmartPhoneResult> CreateAsync(int userId, CancellationToken ct = default);
    Task<SmartPhone?> SetWelcomeAsync(int userId, string text, CancellationToken ct = default);
}
