namespace ArkaCallCenter.Core.Abstractions;

/// <summary>
/// خواندن/نوشتن تنظیمات سراسری. اگر کلید در DB نبود، به مقدار محیطی/پیش‌فرض برمی‌گردد.
/// </summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, string? fallback = null, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<double> GetDoubleAsync(string key, double fallback, CancellationToken ct = default);
    Task SetAsync(string key, string? value, bool isSecret = false, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default);
}
