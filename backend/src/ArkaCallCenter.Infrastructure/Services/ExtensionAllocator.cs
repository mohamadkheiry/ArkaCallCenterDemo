using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// یک داخلی آزاد تصادفی در بازه‌ی ۱۰۰۰–۹۹۹۹ انتخاب می‌کند که در DB استفاده نشده باشد.
/// بازه‌ی ۲۰۰–۳۰۰ برای داخلی‌های انسانی Issabel/SIP رزرو است و در تخصیص دمو استفاده نمی‌شود.
/// یکتایی توسط ایندکس unique روی SmartPhone.Extension تضمین می‌شود.
/// </summary>
public class ExtensionAllocator : IExtensionAllocator
{
    private const int UserMin = 1000;
    private const int UserMax = 9999;
    private const int DemoMin = 1;
    private const int DemoMax = 999;
    private const int HumanSipMin = 200;
    private const int HumanSipMax = 300;

    private readonly ArkaDbContext _db;
    public ExtensionAllocator(ArkaDbContext db) => _db = db;

    public Task<int> AllocateAsync(CancellationToken ct = default) => AllocateInRangeAsync(UserMin, UserMax, ct);
    public Task<int> AllocateDemoAsync(CancellationToken ct = default) =>
        AllocateInRangeAsync(DemoMin, DemoMax, ct, excludeHumanSipRange: true);

    private async Task<int> AllocateInRangeAsync(
        int min,
        int max,
        CancellationToken ct,
        bool excludeHumanSipRange = false)
    {
        var used = await _db.SmartPhones
            .Where(s => s.Extension != null)
            .Select(s => s.Extension!.Value)
            .ToListAsync(ct);
        var usedSet = new HashSet<int>(used);

        bool IsAllowed(int extension) =>
            !excludeHumanSipRange || extension < HumanSipMin || extension > HumanSipMax;

        var availableCapacity = Enumerable.Range(min, max - min + 1).Count(IsAllowed);
        if (usedSet.Count(e => e >= min && e <= max && IsAllowed(e)) >= availableCapacity)
            throw new InvalidOperationException($"هیچ داخلی آزادی در بازه‌ی {min}–{max} باقی نمانده است.");

        for (var i = 0; i < 50; i++)
        {
            var candidate = Random.Shared.Next(min, max + 1);
            if (IsAllowed(candidate) && !usedSet.Contains(candidate)) return candidate;
        }
        for (var e = min; e <= max; e++)
            if (IsAllowed(e) && !usedSet.Contains(e)) return e;

        throw new InvalidOperationException("تخصیص داخلی ناموفق بود.");
    }
}
