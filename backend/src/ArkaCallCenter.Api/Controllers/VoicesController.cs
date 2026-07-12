using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/voices")]
[Authorize]
public class VoicesController : ControllerBase
{
    private readonly ArkaDbContext _db;
    private readonly ISettingsService _settings;
    public VoicesController(ArkaDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    /// <summary>لیست گوینده‌های فعال به‌همراه گوینده‌ی پیش‌فرض و وضعیت نمونه‌صدا.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var voices = await _db.VoiceOptions.AsNoTracking()
            .Where(v => v.Enabled)
            .OrderByDescending(v => v.IsDefault)
            .Select(v => new { v.Name, v.DisplayName, v.IsDefault, hasSample = v.SampleAudioPath != null })
            .ToListAsync(ct);
        var def = await _settings.GetAsync(SettingKeys.DefaultVoiceName, "alloy", ct);
        return Ok(new { voices, defaultVoice = def });
    }

    /// <summary>
    /// استریم نمونه‌صدای یک گوینده. ناشناس، چون تگ audio نمی‌تواند هدر
    /// Authorization بفرستد و محتوا حساس نیست.
    /// </summary>
    [HttpGet("{name}/sample")]
    [AllowAnonymous]
    public async Task<IActionResult> Sample(string name, CancellationToken ct)
    {
        var path = await _db.VoiceOptions.AsNoTracking()
            .Where(v => v.Name == name)
            .Select(v => v.SampleAudioPath)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return NotFound();
        return PhysicalFile(path, "audio/mpeg", enableRangeProcessing: true);
    }
}
