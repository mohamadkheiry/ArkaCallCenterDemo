using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Api.Models;
using ArkaCallCenter.Core.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>خواندن کد یک‌بارمصرف برای شماره موبایل از طریق تماس CodeSenderWithPhone.</summary>
    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp(RequestOtpRequest req, CancellationToken ct)
    {
        var result = await _auth.RequestOtpAsync(req.PhoneNumber, ct);
        return OtpResponse(result);
    }

    /// <summary>خواندنِ کد تأیید از طریق تماس تلفنی (صدای گنجی، رقم‌به‌رقم).</summary>
    [HttpPost("request-otp-call")]
    public async Task<IActionResult> RequestOtpByCall(RequestOtpRequest req, CancellationToken ct)
    {
        var result = await _auth.RequestOtpByCallAsync(req.PhoneNumber, ct);
        return OtpResponse(result);
    }

    /// <summary>اعتبارسنجی کد و صدور توکن.</summary>
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest req, CancellationToken ct)
    {
        var result = await _auth.VerifyOtpAsync(req.PhoneNumber, req.Code, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(new
        {
            token = result.Token,
            isNewUser = result.IsNewUser,
            profileCompleted = result.ProfileCompleted,
        });
    }

    /// <summary>تکمیل پروفایل پس از اولین ورود (نام/نام‌خانوادگی/برند).</summary>
    [Authorize]
    [HttpPost("profile")]
    public async Task<IActionResult> CompleteProfile(CompleteProfileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await _auth.CompleteProfileAsync(userId, req.FirstName, req.LastName, req.BrandName, ct);
        if (user is null) return NotFound(new { error = "کاربر یافت نشد." });
        return Ok(new { user.FirstName, user.LastName, user.BrandName, user.ProfileCompleted });
    }

    private IActionResult OtpResponse(OtpRequestResult result)
    {
        if (result.Success)
            return Ok(new
            {
                message = "تماس برقرار شد؛ کد برایتان خوانده می‌شود.",
                result.RetryAfterSeconds,
            });

        if (result.RetryAfterSeconds > 0)
        {
            Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = result.Error,
                result.RetryAfterSeconds,
            });
        }

        return BadRequest(new { error = result.Error });
    }
}
