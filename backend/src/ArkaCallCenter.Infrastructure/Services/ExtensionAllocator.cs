using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// یک داخلی آزاد تصادفی در بازه‌ی ۱۰۰۰–۹۹۹۹ انتخاب می‌کند که در DB استفاده نشده باشد.
/// یکتایی توسط ایندکس unique روی SmartPhone.Extension تضمین می‌شود.
/// </summary>
public class ExtensionAllocator : IExtensionAllocator
{
    private const int Min = 1000;
    private const int Max = 9999;

    private readonly ArkaDbContext _db;
    public ExtensionAllocator(ArkaDbContext db) => _db = db;

    public async Task<int> AllocateAsync(CancellationToken ct = default)
    {
        var used = await _db.SmartPhones
            .Where(s => s.Extension != null)
            .Select(s => s.Extension!.Value)
            .ToListAsync(ct);
        var usedSet = new HashSet<int>(used);
        var total = Max - Min + 1;
        if (usedSet.Count >= total)
            throw new InvalidOperationException("هیچ داخلی آزادی در بازه‌ی ۱۰۰۰–۹۹۹۹ باقی نمانده است.");

        // چند تلاش تصادفی؛ در صورت شکست، اولین آزاد را برمی‌داریم.
        for (var i = 0; i < 50; i++)
        {
            var candidate = Random.Shared.Next(Min, Max + 1);
            if (!usedSet.Contains(candidate)) return candidate;
        }
        for (var e = Min; e <= Max; e++)
            if (!usedSet.Contains(e)) return e;

        throw new InvalidOperationException("تخصیص داخلی ناموفق بود.");
    }
}
