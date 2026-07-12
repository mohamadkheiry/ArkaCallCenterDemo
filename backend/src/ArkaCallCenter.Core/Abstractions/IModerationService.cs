namespace ArkaCallCenter.Core.Abstractions;

public record ModerationResult(bool Allowed, string? Reason);

/// <summary>
/// بررسی انطباق محتوای پایگاه دانش با قوانین جمهوری اسلامی ایران.
/// در صورت مغایرت، Allowed=false و Reason دلیل را برمی‌گرداند.
/// </summary>
public interface IModerationService
{
    Task<ModerationResult> CheckAsync(string content, CancellationToken ct = default);
}
