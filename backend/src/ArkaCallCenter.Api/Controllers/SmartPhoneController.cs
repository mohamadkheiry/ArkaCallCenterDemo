using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Core.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/smartphone")]
[Authorize]
public class SmartPhoneController : ControllerBase
{
    private readonly ISmartPhoneService _service;
    public SmartPhoneController(ISmartPhoneService service) => _service = service;

    public record WelcomeRequest(string Text);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var sp = await _service.GetAsync(User.GetUserId(), ct);
        if (sp is null) return Ok(null);
        return Ok(new
        {
            extension = sp.Extension,
            status = sp.Status.ToString(),
            sp.WelcomeMessageText,
            hasWelcomeAudio = !string.IsNullOrEmpty(sp.WelcomeAudioPath),
        });
    }

    /// <summary>ثبت/به‌روزرسانی پیام خوش‌آمد و تولید نسخه‌ی صوتی آن.</summary>
    [HttpPut("welcome")]
    public async Task<IActionResult> SetWelcome(WelcomeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { error = "متن پیام خوش‌آمد نمی‌تواند خالی باشد." });
        var sp = await _service.SetWelcomeAsync(User.GetUserId(), req.Text, ct);
        return Ok(new { sp!.WelcomeMessageText, hasWelcomeAudio = !string.IsNullOrEmpty(sp.WelcomeAudioPath) });
    }

    /// <summary>ساخت تلفن هوشمند: تخصیص داخلی، provisioning روی ایزابل و ارسال پیامک.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var result = await _service.CreateAsync(User.GetUserId(), ct);
        if (!result.Ok) return BadRequest(new { error = result.Error });
        return Ok(new
        {
            extension = result.SmartPhone!.Extension,
            status = result.SmartPhone.Status.ToString(),
            message = "تلفن هوشمند با موفقیت ساخته شد.",
        });
    }
}
