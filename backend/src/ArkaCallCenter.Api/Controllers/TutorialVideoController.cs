using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkaCallCenter.Api.Controllers;

/// <summary>
/// پخش ویدیوی آموزشی برای کاربران. استریم به‌صورت ناشناس است چون تگ ویدیو
/// نمی‌تواند هدر Authorization بفرستد و محتوا حساس نیست.
/// </summary>
[ApiController]
[Route("api/tutorial-video")]
public class TutorialVideoController : ControllerBase
{
    private readonly ISettingsService _settings;
    public TutorialVideoController(ISettingsService settings) => _settings = settings;

    [HttpGet("info")]
    [AllowAnonymous]
    public async Task<IActionResult> Info(CancellationToken ct)
    {
        var path = await _settings.GetAsync(SettingKeys.TutorialVideoPath, null, ct);
        var available = !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        return Ok(new { available });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Stream(CancellationToken ct)
    {
        var path = await _settings.GetAsync(SettingKeys.TutorialVideoPath, null, ct);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return NotFound();
        var contentType = path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4";
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }
}
