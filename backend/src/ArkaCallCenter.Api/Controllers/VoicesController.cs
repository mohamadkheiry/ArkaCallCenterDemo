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

    /// <summary>لیست گوینده‌های فعال به‌همراه گوینده‌ی پیش‌فرض.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var voices = await _db.VoiceOptions.AsNoTracking()
            .Where(v => v.Enabled)
            .OrderByDescending(v => v.IsDefault)
            .Select(v => new { v.Name, v.DisplayName, v.IsDefault })
            .ToListAsync(ct);
        var def = await _settings.GetAsync(SettingKeys.DefaultVoiceName, "alloy", ct);
        return Ok(new { voices, defaultVoice = def });
    }
}
