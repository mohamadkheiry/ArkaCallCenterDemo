using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/knowledge-base")]
[Authorize]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IKnowledgeBaseService _kb;
    private readonly IKnowledgeAnswerService _answers;
    private readonly ArkaDbContext _db;
    public KnowledgeBaseController(IKnowledgeBaseService kb, IKnowledgeAnswerService answers, ArkaDbContext db)
    {
        _kb = kb;
        _answers = answers;
        _db = db;
    }

    public record SetTextRequest(string Text);
    public record AnswerRequest(string Question, string Answer);
    public record FallbackRequest(string Text);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var kb = await _kb.GetAsync(userId, ct);
        if (kb is null) return Ok(null);
        var answerCount = await _db.KnowledgeAnswers.CountAsync(item => item.KnowledgeBase.UserId == userId, ct);
        return Ok(new
        {
            sourceType = kb.SourceType.ToString(),
            rawText = kb.RawText,
            fileName = kb.FileName,
            charCount = kb.CharCount,
            fileSizeBytes = kb.FileSizeBytes,
            moderationStatus = kb.ModerationStatus.ToString(),
            updatedAt = kb.UpdatedAt ?? kb.CreatedAt,
            answerCount,
            legacyContentPreserved = !string.IsNullOrWhiteSpace(kb.RawText),
        });
    }

    [HttpGet("answers")]
    public async Task<IActionResult> GetAnswers([FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await _answers.ListAsync(User.GetUserId(), skip, take, ct));

    [HttpPost("answers")]
    public async Task<IActionResult> AddAnswer(AnswerRequest request, CancellationToken ct)
    {
        var result = await _answers.AddAsync(User.GetUserId(), request.Question, request.Answer, ct);
        return result.Ok ? Ok(result.Item) : BadRequest(new { error = result.Error });
    }

    [HttpPut("answers/{id:int}")]
    public async Task<IActionResult> UpdateAnswer(int id, AnswerRequest request, CancellationToken ct)
    {
        var result = await _answers.UpdateAsync(User.GetUserId(), id, request.Question, request.Answer, ct);
        return result.Ok ? Ok(result.Item) : BadRequest(new { error = result.Error });
    }

    [HttpDelete("answers/{id:int}")]
    public async Task<IActionResult> DeleteAnswer(int id, CancellationToken ct)
    {
        await _answers.DeleteAsync(User.GetUserId(), id, ct);
        return Ok(new { message = "سؤال و پاسخ حذف شد." });
    }

    [HttpPost("answers/{id:int}/regenerate-audio")]
    public async Task<IActionResult> RegenerateAnswerAudio(int id, CancellationToken ct)
    {
        var result = await _answers.RegenerateAudioAsync(User.GetUserId(), id, ct);
        return result.Ok ? Ok(result.Item) : BadRequest(new { error = result.Error });
    }

    [HttpGet("answers/{id:int}/audio")]
    public async Task<IActionResult> GetAnswerAudio(int id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var path = await _db.KnowledgeAnswers.AsNoTracking()
            .Where(item => item.Id == id && item.KnowledgeBase.UserId == userId)
            .Select(item => item.AudioPath).FirstOrDefaultAsync(ct);
        return HasPlayableWav(path)
            ? PhysicalFile(path!, "audio/wav", enableRangeProcessing: true)
            : NotFound(new { error = "فایل صوتی پاسخ موجود یا معتبر نیست." });
    }

    [HttpGet("fallback")]
    public async Task<IActionResult> GetFallback(CancellationToken ct)
        => Ok(await _answers.GetFallbackAsync(User.GetUserId(), ct));

    [HttpPut("fallback")]
    public async Task<IActionResult> SetFallback(FallbackRequest request, CancellationToken ct)
    {
        var result = await _answers.SetFallbackAsync(User.GetUserId(), request.Text, ct);
        return result.Ok ? Ok(result) : BadRequest(new { error = result.Error });
    }

    [HttpGet("fallback/audio")]
    public async Task<IActionResult> GetFallbackAudio(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var path = await _db.KnowledgeBases.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.FallbackAudioPath)
            .FirstOrDefaultAsync(ct);
        return HasPlayableWav(path)
            ? PhysicalFile(path!, "audio/wav", enableRangeProcessing: true)
            : NotFound(new { error = "فایل صوتی پیام سؤال بی‌پاسخ موجود یا معتبر نیست." });
    }

    [HttpPost("text")]
    public async Task<IActionResult> SetText(SetTextRequest req, CancellationToken ct)
    {
        var result = await _kb.SetTextAsync(User.GetUserId(), req.Text, ct);
        return result.Ok ? Ok(new { message = "پایگاه دانش ذخیره شد." }) : BadRequest(new { error = result.Error });
    }

    [HttpPost("file")]
    [RequestSizeLimit(1_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SetFile(IFormFile file, CancellationToken ct)
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

    private static bool HasPlayableWav(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return false;
        try
        {
            using var stream = System.IO.File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            return stream.Length > 44 && stream.Read(header) == 12 &&
                   header[..4].SequenceEqual("RIFF"u8) && header[8..].SequenceEqual("WAVE"u8);
        }
        catch { return false; }
    }
}
