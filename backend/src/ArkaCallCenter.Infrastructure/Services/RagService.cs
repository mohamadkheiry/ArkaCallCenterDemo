using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Infrastructure.Services;

public class RagService : IRagService
{
    private const double LexicalRelevanceBoost = 0.08;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "این", "آن", "است", "هست", "بود", "برای", "چند", "چقدر", "چیست", "چیه", "آیا",
        "قبل", "بعد", "باید", "شود", "شده", "کردن", "کنم", "کنیم", "درباره", "یعنی",
        "the", "what", "how", "and", "for", "is", "are"
    };
    private readonly ArkaDbContext _db;
    private readonly IOpenAiService _openai;
    private readonly ISettingsService _settings;

    public RagService(ArkaDbContext db, IOpenAiService openai, ISettingsService settings)
    {
        _db = db;
        _openai = openai;
        _settings = settings;
    }

    public async Task IndexAsync(KnowledgeBase kb, CancellationToken ct = default)
    {
        // پاک‌سازی chunkهای قبلی
        var old = await _db.KnowledgeChunks.Where(c => c.KnowledgeBaseId == kb.Id).ToListAsync(ct);
        if (old.Count > 0) _db.KnowledgeChunks.RemoveRange(old);

        var text = kb.RawText ?? "";
        var chunks = Chunk(text, KbLimits.ChunkSize, KbLimits.ChunkOverlap);
        if (chunks.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
            return;
        }

        var embeddings = await _openai.EmbedBatchAsync(chunks, ct);
        for (var i = 0; i < chunks.Count; i++)
        {
            _db.KnowledgeChunks.Add(new KnowledgeChunk
            {
                KnowledgeBaseId = kb.Id,
                ChunkIndex = i,
                Content = chunks[i],
                EmbeddingJson = JsonSerializer.Serialize(embeddings[i]),
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RagAnswer> RetrieveAsync(int userId, string query, CancellationToken ct = default)
    {
        var threshold = await _settings.GetDoubleAsync(SettingKeys.RagSimilarityThreshold, 0.35, ct);
        var topK = await _settings.GetIntAsync(SettingKeys.RagTopK, 4, ct);

        var chunks = await _db.KnowledgeChunks
            .AsNoTracking()
            .Where(c => c.KnowledgeBase.UserId == userId)
            .ToListAsync(ct);

        if (chunks.Count == 0)
            return new RagAnswer(false, Array.Empty<RagHit>(), "");

        var q = await _openai.EmbedAsync(query, ct);

        var scored = chunks
            .Select(c => new RagHit(c.Content, Cosine(q, Deserialize(c.EmbeddingJson))))
            .OrderByDescending(h => h.Score)
            .Take(topK)
            .ToList();

        // Embedding فارسی/انگلیسی گاهی برای عبارت‌های دقیق دو‌زبانه (مثل Progressive Overload)
        // کمی پایین‌تر از آستانه امتیاز می‌دهد. هم‌پوشانی واژه‌ی معنادار فقط یک boost کوچک می‌دهد؛
        // بنابراین سؤال کاملاً نامرتبط همچنان با fallback پاسخ داده می‌شود.
        var best = scored.FirstOrDefault();
        var lexicalBoost = best is not null && HasDistinctiveTermOverlap(query, best.Content)
            ? LexicalRelevanceBoost
            : 0;
        var found = best is not null && best.Score + lexicalBoost >= threshold;
        var context = found ? string.Join("\n---\n", scored.Select(h => h.Content)) : "";
        return new RagAnswer(found, scored, context);
    }

    // ---- helpers ----
    internal static List<string> Chunk(string text, int size, int overlap)
    {
        text = text.Trim();
        var result = new List<string>();
        if (text.Length == 0) return result;
        if (text.Length <= size)
        {
            result.Add(text);
            return result;
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + size, text.Length);
            result.Add(text.Substring(start, end - start).Trim());
            if (end >= text.Length) break;
            start = end - overlap;
            if (start < 0) start = 0;
        }
        return result;
    }

    private static float[] Deserialize(string json)
        => JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();

    internal static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    internal static bool HasDistinctiveTermOverlap(string query, string content)
    {
        var queryTokens = DistinctiveTokens(query);
        if (queryTokens.Count == 0) return false;
        var contentTokens = DistinctiveTokens(content);
        return queryTokens.Overlaps(contentTokens);
    }

    private static HashSet<string> DistinctiveTokens(string text)
    {
        var normalized = text
            .Normalize(NormalizationForm.FormKC)
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .ToLowerInvariant();

        return Regex.Split(normalized, @"[^\p{L}\p{N}]+")
            .Where(token => token.Length >= 3 && !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
