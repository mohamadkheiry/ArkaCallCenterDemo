using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

public sealed partial class KnowledgeAnswerService : IKnowledgeAnswerService
{
    public const int MaxQuestionLength = 500;
    public const int MaxAnswerLength = 4_000;
    public const int MaxFallbackLength = 1_500;

    private readonly ArkaDbContext _db;
    private readonly IGapAiService _gap;
    private readonly IModerationService _moderation;
    private readonly ISettingsService _settings;
    private readonly ILogger<KnowledgeAnswerService> _logger;
    private readonly string _uploadsPath;

    public KnowledgeAnswerService(
        ArkaDbContext db,
        IGapAiService gap,
        IModerationService moderation,
        ISettingsService settings,
        IConfiguration configuration,
        ILogger<KnowledgeAnswerService> logger)
    {
        _db = db;
        _gap = gap;
        _moderation = moderation;
        _settings = settings;
        _logger = logger;
        _uploadsPath = configuration["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<KnowledgeAnswerPage> ListAsync(
        int userId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);
        var query = _db.KnowledgeAnswers.AsNoTracking()
            .Where(item => item.KnowledgeBase.UserId == userId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(item => item.SortOrder).ThenBy(item => item.Id)
            .Skip(skip).Take(take).ToListAsync(ct);
        return new KnowledgeAnswerPage(total, rows.Select(Map).ToList());
    }

    public async Task<KnowledgeAnswerResult> AddAsync(
        int userId, string question, string answer, CancellationToken ct = default)
    {
        var validation = Validate(question, answer);
        if (validation is not null) return new KnowledgeAnswerResult(false, validation, null);
        question = question.Trim();
        answer = answer.Trim();
        var normalizedQuestion = NormalizeQuestion(question);

        if (await _db.KnowledgeAnswers.AsNoTracking().AnyAsync(
                item => item.KnowledgeBase.UserId == userId &&
                        item.NormalizedQuestion == normalizedQuestion, ct))
            return new KnowledgeAnswerResult(false, "این سؤال قبلاً ثبت شده است؛ همان مورد را ویرایش کنید.", null);

        var moderation = await _moderation.CheckAsync($"سؤال: {question}\nپاسخ: {answer}", ct);
        if (!moderation.Allowed)
            return new KnowledgeAnswerResult(false, moderation.Reason ?? "محتوا قابل ذخیره نیست.", null);

        byte[] wav;
        try { wav = await _gap.GenerateSpeechWav8kAsync(answer, ct: ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not generate answer audio for user {UserId}.", userId);
            return new KnowledgeAnswerResult(false, "تولید صوت پاسخ انجام نشد؛ سؤال و پاسخ ذخیره نشدند.", null);
        }

        var kb = await GetOrCreateKnowledgeBaseAsync(userId, ct);
        var order = await _db.KnowledgeAnswers
            .Where(item => item.KnowledgeBaseId == kb.Id)
            .Select(item => (int?)item.SortOrder).MaxAsync(ct) ?? -1;
        var path = BuildAudioPath("qa", userId);
        await WriteAtomicallyAsync(path, wav, ct);
        var entity = new KnowledgeAnswer
        {
            KnowledgeBaseId = kb.Id,
            Question = question,
            NormalizedQuestion = normalizedQuestion,
            Answer = answer,
            SortOrder = order + 1,
            AudioPath = path,
            AudioHash = Hash(answer),
            AudioStatus = KnowledgeAnswerAudioStatus.Ready,
        };
        _db.KnowledgeAnswers.Add(entity);
        kb.SourceType = KbSourceType.QuestionAnswer;
        kb.ModerationStatus = ModerationStatus.Approved;
        kb.UpdatedAt = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(ct); }
        catch
        {
            TryDeleteOwnedAudio(path);
            throw;
        }
        return new KnowledgeAnswerResult(true, null, Map(entity));
    }

    public async Task<KnowledgeAnswerResult> UpdateAsync(
        int userId, int id, string question, string answer, CancellationToken ct = default)
    {
        var validation = Validate(question, answer);
        if (validation is not null) return new KnowledgeAnswerResult(false, validation, null);
        var entity = await _db.KnowledgeAnswers
            .FirstOrDefaultAsync(item => item.Id == id && item.KnowledgeBase.UserId == userId, ct);
        if (entity is null) return new KnowledgeAnswerResult(false, "سؤال پیدا نشد.", null);

        question = question.Trim();
        answer = answer.Trim();
        var normalizedQuestion = NormalizeQuestion(question);
        if (await _db.KnowledgeAnswers.AsNoTracking().AnyAsync(
                item => item.Id != id && item.KnowledgeBase.UserId == userId &&
                        item.NormalizedQuestion == normalizedQuestion, ct))
            return new KnowledgeAnswerResult(false, "این سؤال قبلاً ثبت شده است؛ مورد تکراری ایجاد نمی‌شود.", null);

        var moderation = await _moderation.CheckAsync($"سؤال: {question}\nپاسخ: {answer}", ct);
        if (!moderation.Allowed)
            return new KnowledgeAnswerResult(false, moderation.Reason ?? "محتوا قابل ذخیره نیست.", null);

        var answerChanged = !string.Equals(entity.Answer, answer, StringComparison.Ordinal);
        string? newPath = null;
        if (answerChanged || !HasPlayableAudio(entity.AudioPath))
        {
            try
            {
                var wav = await _gap.GenerateSpeechWav8kAsync(answer, ct: ct);
                newPath = BuildAudioPath("qa", userId);
                await WriteAtomicallyAsync(newPath, wav, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not regenerate answer audio {AnswerId}.", id);
                return new KnowledgeAnswerResult(false, "تولید صوت پاسخ جدید انجام نشد؛ نسخه قبلی حفظ شد.", null);
            }
        }

        var oldPath = entity.AudioPath;
        entity.Question = question;
        entity.NormalizedQuestion = normalizedQuestion;
        entity.Answer = answer;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.AudioError = null;
        entity.AudioStatus = KnowledgeAnswerAudioStatus.Ready;
        if (newPath is not null)
        {
            entity.AudioPath = newPath;
            entity.AudioHash = Hash(answer);
        }
        try { await _db.SaveChangesAsync(ct); }
        catch
        {
            if (newPath is not null) TryDeleteOwnedAudio(newPath);
            throw;
        }
        if (newPath is not null) TryDeleteOwnedAudio(oldPath);
        return new KnowledgeAnswerResult(true, null, Map(entity));
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken ct = default)
    {
        var entity = await _db.KnowledgeAnswers
            .FirstOrDefaultAsync(item => item.Id == id && item.KnowledgeBase.UserId == userId, ct);
        if (entity is null) return;
        _db.KnowledgeAnswers.Remove(entity);
        await _db.SaveChangesAsync(ct);
        TryDeleteOwnedAudio(entity.AudioPath);
    }

    public async Task<KnowledgeAnswerResult> RegenerateAudioAsync(
        int userId, int id, CancellationToken ct = default)
    {
        var entity = await _db.KnowledgeAnswers
            .FirstOrDefaultAsync(item => item.Id == id && item.KnowledgeBase.UserId == userId, ct);
        if (entity is null) return new KnowledgeAnswerResult(false, "سؤال پیدا نشد.", null);
        var oldPath = entity.AudioPath;
        var oldStatus = entity.AudioStatus;
        var oldError = entity.AudioError;
        string? newPath = null;
        try
        {
            var wav = await _gap.GenerateSpeechWav8kAsync(entity.Answer, ct: ct);
            newPath = BuildAudioPath("qa", userId);
            await WriteAtomicallyAsync(newPath, wav, ct);
            entity.AudioPath = newPath;
            entity.AudioHash = Hash(entity.Answer);
            entity.AudioStatus = KnowledgeAnswerAudioStatus.Ready;
            entity.AudioError = null;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            TryDeleteOwnedAudio(oldPath);
            return new KnowledgeAnswerResult(true, null, Map(entity));
        }
        catch (Exception ex)
        {
            if (newPath is not null) TryDeleteOwnedAudio(newPath);
            entity.AudioPath = oldPath;
            entity.AudioStatus = oldStatus;
            entity.AudioError = oldError;
            if (!HasPlayableAudio(oldPath))
            {
                entity.AudioStatus = KnowledgeAnswerAudioStatus.Failed;
                entity.AudioError = "تولید صوت انجام نشد.";
                entity.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            _logger.LogWarning(ex, "Could not regenerate answer audio {AnswerId}.", id);
            return new KnowledgeAnswerResult(false, "تولید صوت انجام نشد؛ نسخه قبلی حفظ شد.", Map(entity));
        }
    }

    public async Task<KnowledgeFallbackResult> GetFallbackAsync(int userId, CancellationToken ct = default)
    {
        var kb = await _db.KnowledgeBases.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, ct);
        var global = await _settings.GetAsync(SettingKeys.FallbackMessageText,
            ConversationMessages.UnknownKnowledge, ct) ?? ConversationMessages.UnknownKnowledge;
        var text = string.IsNullOrWhiteSpace(kb?.FallbackText) ? global : kb.FallbackText;
        return new KnowledgeFallbackResult(true, null, text, HasPlayableAudio(kb?.FallbackAudioPath),
            kb?.UpdatedAt ?? kb?.CreatedAt);
    }

    public async Task<KnowledgeFallbackResult> SetFallbackAsync(
        int userId, string text, CancellationToken ct = default)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return new KnowledgeFallbackResult(false, "پیام سؤال بی‌پاسخ خالی است.", null, false);
        if (text.Length > MaxFallbackLength)
            return new KnowledgeFallbackResult(false, $"پیام باید حداکثر {MaxFallbackLength} کاراکتر باشد.", null, false);
        var existingKb = await _db.KnowledgeBases.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (existingKb is not null && string.Equals(existingKb.FallbackText, text, StringComparison.Ordinal) &&
            HasPlayableAudio(existingKb.FallbackAudioPath))
            return new KnowledgeFallbackResult(true, null, text, true, existingKb.UpdatedAt ?? existingKb.CreatedAt);

        var moderation = await _moderation.CheckAsync(text, ct);
        if (!moderation.Allowed)
            return new KnowledgeFallbackResult(false, moderation.Reason ?? "محتوا قابل ذخیره نیست.", null, false);

        byte[] wav;
        try { wav = await _gap.GenerateSpeechWav8kAsync(text, ct: ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not generate fallback audio for user {UserId}.", userId);
            return new KnowledgeFallbackResult(false, "تولید صوت پیام سؤال بی‌پاسخ انجام نشد؛ نسخه قبلی حفظ شد.", null, false);
        }
        var kb = existingKb ?? await GetOrCreateKnowledgeBaseAsync(userId, ct);
        var path = BuildAudioPath("fallback", userId);
        await WriteAtomicallyAsync(path, wav, ct);
        var oldPath = kb.FallbackAudioPath;
        kb.FallbackText = text;
        kb.FallbackAudioPath = path;
        kb.FallbackAudioHash = Hash(text);
        kb.SourceType = KbSourceType.QuestionAnswer;
        kb.UpdatedAt = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(ct); }
        catch
        {
            TryDeleteOwnedAudio(path);
            throw;
        }
        TryDeleteOwnedAudio(oldPath);
        return new KnowledgeFallbackResult(true, null, text, true, kb.UpdatedAt ?? kb.CreatedAt);
    }

    public async Task<KnowledgeAnswerMatch> MatchAsync(
        int userId, string question, CancellationToken ct = default)
    {
        var normalized = NormalizeQuestion(question);
        if (normalized.Length == 0) return new KnowledgeAnswerMatch(false, null, null, null, null, 0);
        var exact = await _db.KnowledgeAnswers.AsNoTracking()
            .Where(item => item.KnowledgeBase.UserId == userId &&
                           item.NormalizedQuestion == normalized &&
                           item.AudioStatus == KnowledgeAnswerAudioStatus.Ready)
            .Select(item => new { item.Id, item.Question, item.Answer, item.AudioPath })
            .FirstOrDefaultAsync(ct);
        if (exact is not null)
            return HasPlayableAudio(exact.AudioPath)
                ? new KnowledgeAnswerMatch(true, exact.Id, exact.Question, exact.Answer, exact.AudioPath, 1)
                : new KnowledgeAnswerMatch(false, null, null, null, null, 1);

        var candidates = await _db.KnowledgeAnswers.AsNoTracking()
            .Where(item => item.KnowledgeBase.UserId == userId &&
                           item.AudioStatus == KnowledgeAnswerAudioStatus.Ready)
            .Select(item => new { item.Id, item.Question, item.NormalizedQuestion, item.Answer, item.AudioPath })
            .ToListAsync(ct);

        var ranked = candidates
            .Select(item => new { Item = item, Score = Similarity(normalized, item.NormalizedQuestion) })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Item.Id)
            .ToList();
        var best = ranked.FirstOrDefault();
        var threshold = normalized.Length <= 12 ? 0.78 : 0.68;
        if (best is null)
            return new KnowledgeAnswerMatch(false, null, null, null, null, 0);

        var semanticConfirmed = false;
        if (best.Score < threshold)
        {
            try
            {
                // Always run the semantic judge when deterministic matching is not conclusive.
                // A 48-item shortlist keeps the call payload bounded while covering paraphrases
                // that share few literal Persian tokens with the stored wording.
                var shortlist = ranked.Take(48)
                    .Select(item => new GapQuestionCandidate(item.Item.Id, item.Item.Question)).ToList();
                var selectedId = await _gap.SelectMatchingQuestionAsync(question, shortlist, ct);
                var semantic = selectedId is null ? null : ranked.FirstOrDefault(item => item.Item.Id == selectedId.Value);
                if (semantic is not null)
                {
                    best = semantic;
                    semanticConfirmed = true;
                }
            }
            catch (Exception ex)
            {
                // Semantic verification is an accuracy enhancement; deterministic matching remains available.
                _logger.LogWarning(ex, "GapGPT semantic question selection failed for user {UserId}.", userId);
            }
        }

        var accepted = best.Score >= threshold || semanticConfirmed;
        if (!accepted || !HasPlayableAudio(best.Item.AudioPath))
            return new KnowledgeAnswerMatch(false, null, null, null, null, best?.Score ?? 0);
        return new KnowledgeAnswerMatch(true, best.Item.Id, best.Item.Question, best.Item.Answer,
            best.Item.AudioPath, best.Score);
    }

    public static string NormalizeQuestion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var builder = new StringBuilder(value.Length);
        foreach (var raw in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormKC))
        {
            var ch = raw switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                '\u200c' or '\u200d' => ' ',
                >= '۰' and <= '۹' => (char)('0' + raw - '۰'),
                >= '٠' and <= '٩' => (char)('0' + raw - '٠'),
                _ => raw,
            };
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)) builder.Append(ch);
        }
        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    public static double Similarity(string normalizedQuestion, string normalizedCandidate)
    {
        if (normalizedQuestion == normalizedCandidate) return 1;
        if (normalizedQuestion.Length == 0 || normalizedCandidate.Length == 0) return 0;
        var containment = normalizedQuestion.Contains(normalizedCandidate, StringComparison.Ordinal) ||
                          normalizedCandidate.Contains(normalizedQuestion, StringComparison.Ordinal)
            ? (double)Math.Min(normalizedQuestion.Length, normalizedCandidate.Length) /
              Math.Max(normalizedQuestion.Length, normalizedCandidate.Length)
            : 0;
        var tokenDice = Dice(Tokens(normalizedQuestion), Tokens(normalizedCandidate));
        var trigramDice = Dice(Ngrams(normalizedQuestion, 3), Ngrams(normalizedCandidate, 3));
        var edit = NormalizedEditSimilarity(normalizedQuestion, normalizedCandidate);
        return Math.Max(containment, Math.Max(tokenDice * 0.55 + trigramDice * 0.45,
            edit * 0.55 + trigramDice * 0.45));
    }

    private async Task<KnowledgeBase> GetOrCreateKnowledgeBaseAsync(int userId, CancellationToken ct)
    {
        var kb = await _db.KnowledgeBases.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (kb is not null) return kb;
        kb = new KnowledgeBase
        {
            UserId = userId,
            SourceType = KbSourceType.QuestionAnswer,
            RawText = null,
            ModerationStatus = ModerationStatus.Approved,
        };
        _db.KnowledgeBases.Add(kb);
        await _db.SaveChangesAsync(ct);
        return kb;
    }

    private static string? Validate(string question, string answer)
    {
        question = (question ?? "").Trim();
        answer = (answer ?? "").Trim();
        if (question.Length == 0) return "متن سؤال خالی است.";
        if (answer.Length == 0) return "متن پاسخ خالی است.";
        if (question.Length > MaxQuestionLength) return $"سؤال باید حداکثر {MaxQuestionLength} کاراکتر باشد.";
        if (answer.Length > MaxAnswerLength) return $"پاسخ باید حداکثر {MaxAnswerLength} کاراکتر باشد.";
        return null;
    }

    private static KnowledgeAnswerItem Map(KnowledgeAnswer entity) => new(
        entity.Id, entity.Question, entity.Answer, entity.SortOrder,
        entity.AudioStatus == KnowledgeAnswerAudioStatus.Ready && !HasPlayableAudio(entity.AudioPath)
            ? KnowledgeAnswerAudioStatus.Failed
            : entity.AudioStatus,
        entity.AudioStatus == KnowledgeAnswerAudioStatus.Ready && !HasPlayableAudio(entity.AudioPath)
            ? "فایل صوتی موجود نیست؛ صوت را بازتولید کنید."
            : entity.AudioError,
        entity.UpdatedAt ?? entity.CreatedAt);

    private string BuildAudioPath(string prefix, int userId)
        => Path.Combine(_uploadsPath, $"{prefix}_{userId}_{Guid.NewGuid():N}.wav");

    private static async Task WriteAtomicallyAsync(string path, byte[] bytes, CancellationToken ct)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, ct);
            File.Move(temp, path, true);
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
    }

    private void TryDeleteOwnedAudio(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(_uploadsPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) File.Delete(full);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Could not remove superseded QA audio {Path}.", path); }
    }

    private static bool HasPlayableAudio(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            return stream.Length > 44 && stream.Read(header) == 12 &&
                   header[..4].SequenceEqual("RIFF"u8) && header[8..].SequenceEqual("WAVE"u8);
        }
        catch { return false; }
    }

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static HashSet<string> Tokens(string value)
        => value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> Ngrams(string value, int size)
    {
        var compact = value.Replace(" ", "", StringComparison.Ordinal);
        if (compact.Length <= size) return new HashSet<string>(new[] { compact }, StringComparer.Ordinal);
        return Enumerable.Range(0, compact.Length - size + 1)
            .Select(index => compact.Substring(index, size)).ToHashSet(StringComparer.Ordinal);
    }

    private static double Dice(HashSet<string> first, HashSet<string> second)
    {
        if (first.Count == 0 || second.Count == 0) return 0;
        var overlap = first.Count <= second.Count ? first.Count(second.Contains) : second.Count(first.Contains);
        return 2d * overlap / (first.Count + second.Count);
    }

    private static double NormalizedEditSimilarity(string first, string second)
    {
        if (first.Length > 600 || second.Length > 600) return 0;
        var previous = Enumerable.Range(0, second.Length + 1).ToArray();
        var current = new int[second.Length + 1];
        for (var i = 1; i <= first.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= second.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (first[i - 1] == second[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return 1d - previous[second.Length] / (double)Math.Max(first.Length, second.Length);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
