using ArkaCallCenter.Core.Entities;

namespace ArkaCallCenter.Core.Abstractions;

public record VerifyOtpResult(bool Success, string? Token, bool IsNewUser, bool ProfileCompleted, string? Error);

public interface IAuthService
{
    /// <summary>تولید و ارسال کد OTP برای شماره‌ی موبایل.</summary>
    Task<(bool ok, string? error)> RequestOtpAsync(string phoneNumber, CancellationToken ct = default);

    /// <summary>اعتبارسنجی کد و صدور JWT.</summary>
    Task<VerifyOtpResult> VerifyOtpAsync(string phoneNumber, string code, CancellationToken ct = default);

    /// <summary>تکمیل پروفایل (نام/نام‌خانوادگی/برند) پس از اولین ورود.</summary>
    Task<User?> CompleteProfileAsync(int userId, string firstName, string lastName, string brandName, CancellationToken ct = default);
}
