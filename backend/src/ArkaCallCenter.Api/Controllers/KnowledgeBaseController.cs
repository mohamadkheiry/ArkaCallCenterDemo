using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/knowledge-base")]
[Authorize]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IKnowledgeBaseService _kb;
    public KnowledgeBaseController(IKnowledgeBaseService kb) => _kb = kb;

    public record SetTextRequest(string Text);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var kb = await _kb.GetAsync(User.GetUserId(), ct);
        if (kb is null) return Ok(null);
        return Ok(new
        {
            sourceType = kb.SourceType.ToString(),
            rawText = kb.RawText,
            fileName = kb.FileName,
            charCount = kb.CharCount,
            fileSizeBytes = kb.FileSizeBytes,
            moderationStatus = kb.ModerationStatus.ToString(),
            updatedAt = kb.UpdatedAt ?? kb.CreatedAt,
        });
    }

    [HttpPost("text")]
    public async Task<IActionResult> SetText(SetTextRequest req, CancellationToken ct)
    {
        var result = await _kb.SetTextAsync(User.GetUserId(), req.Text, ct);
        return result.Ok ? Ok(new { message = "پایگاه دانش ذخیره شد." }) : BadRequest(new { error = result.Error });
    }

    [HttpPost("file")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> SetFile([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "فایلی ارسال نشد." });
        if (file.Length > KbLimits.MaxFileBytes)
            return BadRequest(new { error = "حجم فایل باید حداکثر ۱۰۰ کیلوبایت باشد." });

        await using var stream = file.OpenReadStream();
        var result = await _kb.SetFileAsync(User.GetUserId(), file.FileName, file.ContentType, stream, file.Length, ct);
        return result.Ok ? Ok(new { message = "فایل با موفقیت پردازش و ذخیره شد." }) : BadRequest(new { error = result.Error });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await _kb.DeleteAsync(User.GetUserId(), ct);
        return Ok(new { message = "پایگاه دانش حذف شد." });
    }
}
