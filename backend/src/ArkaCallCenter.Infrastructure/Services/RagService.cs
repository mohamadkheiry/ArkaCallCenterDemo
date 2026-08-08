using System.Collections.Concurrent;
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
    private const double MaxLexicalRelevanceBoost = 0.12;
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> ReindexLocks = new();
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "این", "آن", "است", "هست", "بود", "برای", "چند", "چقدر", "چیست", "چیه", "آیا",
        "قبل", "بعد", "باید", "شود", "شده", "کردن", "کنم", "کنیم", "درباره", "یعنی", "لطفا", "لطفاً",
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

        var embeddingModel = await CurrentEmbeddingModelAsync(ct);
        var embeddings = await _openai.EmbedBatchAsync(chunks, ct);
        for (var i = 0; i < chunks.Count; i++)
        {
            _db.KnowledgeChunks.Add(new KnowledgeChunk
            {
                KnowledgeBaseId = kb.Id,
                ChunkIndex = i,
                Content = chunks[i],
                // نام مدل کنار بردار ذخیره می‌شود تا تغییر مدل Embedding باعث مقایسه‌ی
                // بردارهای متعلق به دو فضای متفاوت و fallback اشتباه نشود.
                EmbeddingJson = SerializeEmbedding(embeddingModel, embeddings[i]),
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RagAnswer> RetrieveAsync(int userId, string query, CancellationToken ct = default)
    {
        var threshold = Math.Clamp(
            await _settings.GetDoubleAsync(SettingKeys.RagSimilarityThreshold, 0.35, ct),
            0.20,
            0.85);
        var topK = Math.Clamp(await _settings.GetIntAsync(SettingKeys.RagTopK, 4, ct), 1, 8);

        await EnsureIndexAsync(userId, ct);
        var chunks = await LoadChunksAsync(userId, ct);
        if (chunks.Count == 0)
            return new RagAnswer(false, Array.Empty<RagHit>(), "");

        var q = await _openai.EmbedAsync(query, ct);

        var candidates = chunks
            .Select(c =>
            {
                var semanticScore = Cosine(q, DeserializeEmbedding(c.EmbeddingJson).Vector);
                var lexicalScore = LexicalSimilarity(query, c.Content);
                var hybridScore = semanticScore + Math.Min(MaxLexicalRelevanceBoost,
                    lexicalScore * MaxLexicalRelevanceBoost);
                return new RagCandidate(c.Content, semanticScore, lexicalScore, hybridScore);
            })
            .OrderByDescending(candidate => candidate.HybridScore)
            .Take(topK)
            .ToList();

        // امتیاز معنایی مبناست و شباهت واژگانی فقط تا ۱۲ صدم کمک می‌کند. این ترکیب
        // بازنویسی‌های طبیعی فارسی را می‌پذیرد، بدون آن‌که یک واژه‌ی عمومی به‌تنهایی
        // محتوای کاملاً نامرتبط را معتبر کند.
        var best = candidates.FirstOrDefault();
        var found = best is not null && IsRelevant(
            best.SemanticScore,
            best.LexicalScore,
            threshold);

        var contextCandidates = found
            ? candidates.Where(candidate => candidate.HybridScore >= best!.HybridScore - 0.12)
            : Enumerable.Empty<RagCandidate>();
        var context = found
            ? string.Join("\n---\n", contextCandidates.Select(candidate => candidate.Content))
            : "";
        var hits = candidates
            .Select(candidate => new RagHit(candidate.Content, candidate.HybridScore))
            .ToList();
        return new RagAnswer(found, hits, context);
    }

    public async Task EnsureIndexAsync(int userId, CancellationToken ct = default)
    {
        var chunks = await LoadChunksAsync(userId, ct);
        var embeddingModel = await CurrentEmbeddingModelAsync(ct);

        // نسخه‌های قدیمی فقط آرایه‌ی بردار را ذخیره می‌کردند. همچنین ممکن است مدیر مدل
        // embedding را تغییر داده باشد. در هر دو حالت ایندکس با مدل فعلی بازسازی می‌شود.
        if (chunks.Count == 0 || chunks.Any(c => !string.Equals(
                DeserializeEmbedding(c.EmbeddingJson).Model,
                embeddingModel,
                StringComparison.OrdinalIgnoreCase)))
        {
            await ReindexForCurrentModelAsync(userId, embeddingModel, ct);
        }
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

    private Task<List<KnowledgeChunk>> LoadChunksAsync(int userId, CancellationToken ct)
        => _db.KnowledgeChunks
            .AsNoTracking()
            .Where(c => c.KnowledgeBase.UserId == userId)
            .ToListAsync(ct);

    private async Task<string> CurrentEmbeddingModelAsync(CancellationToken ct)
    {
        var model = await _settings.GetAsync(
            SettingKeys.OpenAiEmbeddingModel,
            "text-embedding-3-large",
            ct);
        return string.IsNullOrWhiteSpace(model) ? "text-embedding-3-large" : model.Trim();
    }

    private async Task<List<KnowledgeChunk>> ReindexForCurrentModelAsync(
        int userId,
        string embeddingModel,
        CancellationToken ct)
    {
        var knowledgeBase = await _db.KnowledgeBases
            .FirstOrDefaultAsync(kb => kb.UserId == userId, ct);
        if (knowledgeBase is null || string.IsNullOrWhiteSpace(knowledgeBase.RawText))
            return new List<KnowledgeChunk>();

        var gate = ReindexLocks.GetOrAdd(knowledgeBase.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // ممکن است تماس دیگری هنگام انتظار ایندکس را بازسازی کرده باشد.
            var currentChunks = await LoadChunksAsync(userId, ct);
            if (currentChunks.Count > 0 && currentChunks.All(c => string.Equals(
                    DeserializeEmbedding(c.EmbeddingJson).Model,
                    embeddingModel,
                    StringComparison.OrdinalIgnoreCase)))
                return currentChunks;

            await IndexAsync(knowledgeBase, ct);
            return await LoadChunksAsync(userId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string SerializeEmbedding(string model, float[] vector)
        => JsonSerializer.Serialize(new EmbeddingEnvelope(model, vector));

    private static EmbeddingEnvelope DeserializeEmbedding(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var legacyVector = document.RootElement
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();
            return new EmbeddingEnvelope(null, legacyVector);
        }

        var root = document.RootElement;
        var model = root.TryGetProperty("Model", out var modelElement)
            ? modelElement.GetString()
            : null;
        var vector = root.TryGetProperty("Vector", out var vectorElement)
            ? vectorElement.EnumerateArray().Select(value => value.GetSingle()).ToArray()
            : Array.Empty<float>();
        return new EmbeddingEnvelope(model, vector);
    }

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

    internal static double LexicalSimilarity(string query, string content)
    {
        var queryTokens = DistinctiveTokens(query);
        if (queryTokens.Count == 0) return 0;
        var contentTokens = DistinctiveTokens(content);
        if (contentTokens.Count == 0) return 0;

        var matched = queryTokens.Count(queryToken => contentTokens.Any(contentToken =>
            TokensAreSimilar(queryToken, contentToken)));
        return matched / (double)queryTokens.Count;
    }

    internal static bool IsRelevant(double semanticScore, double lexicalScore, double threshold)
        => semanticScore >= threshold ||
           (lexicalScore >= 0.25 &&
            semanticScore + Math.Min(MaxLexicalRelevanceBoost, lexicalScore * MaxLexicalRelevanceBoost) >= threshold);

    private static bool TokensAreSimilar(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return true;
        if (left.Length >= 5 && right.Length >= 5 &&
            (left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
             right.Contains(left, StringComparison.OrdinalIgnoreCase)))
            return true;

        return left.Length >= 4 && right.Length >= 4 && CharacterTrigramDice(left, right) >= 0.72;
    }

    private static double CharacterTrigramDice(string left, string right)
    {
        var leftTrigrams = CharacterTrigrams(left);
        var rightTrigrams = CharacterTrigrams(right);
        if (leftTrigrams.Count == 0 || rightTrigrams.Count == 0) return 0;
        var intersection = leftTrigrams.Intersect(rightTrigrams).Count();
        return 2d * intersection / (leftTrigrams.Count + rightTrigrams.Count);
    }

    private static HashSet<string> CharacterTrigrams(string value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (value.Length < 3)
        {
            result.Add(value);
            return result;
        }
        for (var i = 0; i <= value.Length - 3; i++)
            result.Add(value.Substring(i, 3));
        return result;
    }

    private static HashSet<string> DistinctiveTokens(string text)
    {
        var normalized = text
            .Normalize(NormalizationForm.FormKC)
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Replace("‌", " ")
            .ToLowerInvariant();

        normalized = Regex.Replace(normalized, "[\\u064B-\\u065F\\u0670]", "");

        return Regex.Split(normalized, @"[^\p{L}\p{N}]+")
            .Where(token => token.Length >= 3 && !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record EmbeddingEnvelope(string? Model, float[] Vector);
    private sealed record RagCandidate(
        string Content,
        double SemanticScore,
        double LexicalScore,
        double HybridScore);
}
