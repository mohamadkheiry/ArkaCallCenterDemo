namespace ArkaCallCenter.Core.Abstractions;

/// <summary>
/// نگه‌دارنده‌ی هویت کاربرِ جاری برای انتساب مصرف توکن (scoped).
/// در API از روی JWT و در worker به‌صورت دستی مقداردهی می‌شود.
/// </summary>
public interface IUsageContext
{
    int? UserId { get; set; }
    string? PhoneNumber { get; set; }
}

/// <summary>ثبت مصرف توکن.</summary>
public interface ITokenUsageTracker
{
    Task RecordAsync(string operation, string model, string apiKeyFingerprint,
        int promptTokens, int completionTokens, int totalTokens, CancellationToken ct = default);
}

/// <summary>ابزار ساخت اثرانگشت ماسک‌شده از کلید API.</summary>
public static class ApiKeyFingerprint
{
    public static string Of(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "unknown";
        var last4 = apiKey.Length >= 4 ? apiKey[^4..] : apiKey;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(apiKey)));
        return $"…{last4} ({hash[..6].ToLowerInvariant()})";
    }
}
