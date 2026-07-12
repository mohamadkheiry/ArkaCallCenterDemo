using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Api.Controllers;

/// <summary>
/// استریم تصاویر عمومی (آواتار کاربران و لوگوی سامانه) برای تگ img.
/// ناشناس، چون تگ img هدر Authorization نمی‌فرستد و این تصاویر حساس نیستند.
/// </summary>
[ApiController]
[AllowAnonymous]
public class MediaController : ControllerBase
{
    private readonly ArkaDbContext _db;
    private readonly ISettingsService _settings;
    public MediaController(ArkaDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "image/jpeg",
    };

    [HttpGet("api/avatars/{userId:int}")]
    public async Task<IActionResult> Avatar(int userId, CancellationToken ct)
    {
        var path = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.AvatarPath).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, ContentType(path));
    }

    [HttpGet("api/branding/logo")]
    public async Task<IActionResult> Logo(CancellationToken ct)
    {
        var path = await _settings.GetAsync(SettingKeys.SystemLogoPath, null, ct);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, ContentType(path));
    }

    [HttpGet("api/branding/logo/info")]
    public async Task<IActionResult> LogoInfo(CancellationToken ct)
    {
        var path = await _settings.GetAsync(SettingKeys.SystemLogoPath, null, ct);
        return Ok(new { available = !string.IsNullOrEmpty(path) && System.IO.File.Exists(path) });
    }
}
