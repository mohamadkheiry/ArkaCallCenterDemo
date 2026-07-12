using System.Globalization;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly ArkaDbContext _db;
    public SettingsService(ArkaDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key, string? fallback = null, CancellationToken ct = default)
    {
        var s = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        return string.IsNullOrEmpty(s?.Value) ? fallback : s.Value;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var v = await GetAsync(key, null, ct);
        return int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    public async Task<double> GetDoubleAsync(string key, double fallback, CancellationToken ct = default)
    {
        var v = await GetAsync(key, null, ct);
        return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    public async Task SetAsync(string key, string? value, bool isSecret = false, CancellationToken ct = default)
    {
        var s = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (s is null)
        {
            s = new AppSetting { Key = key, Value = value, IsSecret = isSecret };
            _db.AppSettings.Add(s);
        }
        else
        {
            s.Value = value;
            s.IsSecret = isSecret;
            s.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.AppSettings.AsNoTracking().ToListAsync(ct);
        // مقادیر حساس ماسک می‌شوند تا در پاسخ API لو نروند.
        return list.ToDictionary(x => x.Key, x => x.IsSecret && !string.IsNullOrEmpty(x.Value) ? "********" : x.Value);
    }
}
