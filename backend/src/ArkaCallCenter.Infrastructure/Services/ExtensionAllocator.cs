using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// یک داخلی آزاد تصادفی در بازه‌ی ۱۰۰۰–۹۹۹۹ برای کاربران عادی انتخاب می‌کند
/// که در DB استفاده نشده باشد. داخلی دمو را سوپرادمین به‌صورت دستی تعیین می‌کند.
/// یکتایی توسط ایندکس unique روی SmartPhone.Extension تضمین می‌شود.
/// </summary>
public class ExtensionAllocator : IExtensionAllocator
{
    private const int UserMin = 1000;
    private const int UserMax = 9999;
    private readonly ArkaDbContext _db;
    public ExtensionAllocator(ArkaDbContext db) => _db = db;

    public Task<int> AllocateAsync(CancellationToken ct = default) => AllocateInRangeAsync(UserMin, UserMax, ct);

    private async Task<int> AllocateInRangeAsync(int min, int max, CancellationToken ct)
    {
        var used = await _db.SmartPhones
            .Where(s => s.Extension != null)
            .Select(s => s.Extension!.Value)
            .ToListAsync(ct);
        var usedSet = new HashSet<int>(used);

        if (usedSet.Count(e => e >= min && e <= max) >= max - min + 1)
            throw new InvalidOperationException($"هیچ داخلی آزادی در بازه‌ی {min}–{max} باقی نمانده است.");

        for (var i = 0; i < 50; i++)
        {
            var candidate = Random.Shared.Next(min, max + 1);
            if (!usedSet.Contains(candidate)) return candidate;
        }
        for (var e = min; e <= max; e++)
            if (!usedSet.Contains(e)) return e;

        throw new InvalidOperationException("تخصیص داخلی ناموفق بود.");
    }
}
