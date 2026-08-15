using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

public class RagService : IRagService
{
    private const double MaxLexicalRelevanceBoost = 0.12;
    private const double SemanticFusionWeight = 0.55;
    private const double LexicalFusionWeight = 0.25;
    private const double RankFusionWeight = 0.20;
    private const double SemanticRankWeight = 0.68;
    private const double LexicalRankWeight = 0.32;
    private const double ReciprocalRankConstant = 60;
    private static readonly TimeSpan QueryEmbeddingTimeout = TimeSpan.FromSeconds(8);
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> ReindexLocks = new();
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "این", "آن", "است", "هست", "بود", "برای", "چند", "چقدر", "چقدره", "چیست", "چیه", "آیا",
        "قبل", "بعد", "باید", "شود", "شده", "کردن", "کنم", "کنیم", "کنید", "کند", "درباره", "یعنی", "لطفا", "لطفاً",
        "من", "ما", "شما", "که", "چه", "کجا", "چطور", "چگونه", "دارم", "دارد", "دارید", "میشه",
        "ممنون", "ممنونم", "تشکر", "متشکرم", "مرسی", "سپاس", "ببخشید",
        "the", "what", "how", "and", "for", "is", "are"
    };
    private static readonly IReadOnlyDictionary<string, string> SynonymCanonical = BuildSynonymMap();
    private readonly ArkaDbContext _db;
    private readonly IOpenAiService _openai;
    private readonly ISettingsService _settings;
    private readonly ILogger<RagService> _logger;

    public RagService(
        ArkaDbContext db,
        IOpenAiService openai,
        ISettingsService settings,
        ILogger<RagService> logger)
    {
        _db = db;
        _openai = openai;
        _settings = settings;
        _logger = logger;
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

        var bm25Scores = Bm25Scores(query, chunks.Select(chunk => chunk.Content).ToList());
        float[]? q;
        try
        {
            using var embeddingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            embeddingCts.CancelAfter(QueryEmbeddingTimeout);
            q = await _openai.EmbedAsync(query, embeddingCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Query embedding exceeded {Seconds}s for user {UserId}; using lexical retrieval.",
                QueryEmbeddingTimeout.TotalSeconds,
                userId);
            q = null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Query embedding failed for user {UserId}; using lexical retrieval.", userId);
            q = null;
        }

        if (q is null)
            return BuildLexicalFallback(query, chunks, bm25Scores, topK);

        var candidates = chunks
            .Select((c, index) =>
            {
                var semanticScore = Cosine(q, DeserializeEmbedding(c.EmbeddingJson).Vector);
                var fuzzyOverlap = LexicalSimilarity(query, c.Content);
                var lexicalScore = Math.Clamp((bm25Scores[index] * 0.70) + (fuzzyOverlap * 0.30), 0, 1);
                return new RagCandidate(index, c.Content, semanticScore, lexicalScore, 0);
            })
            .ToList();

        var semanticRanks = candidates
            .OrderByDescending(candidate => candidate.SemanticScore)
            .Select((candidate, rank) => (candidate.SourceIndex, Rank: rank + 1))
            .ToDictionary(item => item.SourceIndex, item => item.Rank);
        var lexicalRanks = candidates
            .Where(candidate => candidate.LexicalScore > 0)
            .OrderByDescending(candidate => candidate.LexicalScore)
            .Select((candidate, rank) => (candidate.SourceIndex, Rank: rank + 1))
            .ToDictionary(item => item.SourceIndex, item => item.Rank);

        candidates = candidates
            .Select(candidate => candidate with
            {
                HybridScore = FuseScores(
                    candidate.SemanticScore,
                    candidate.LexicalScore,
                    semanticRanks[candidate.SourceIndex],
                    lexicalRanks.GetValueOrDefault(candidate.SourceIndex))
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
            ? candidates.Where(candidate => candidate.HybridScore >= best!.HybridScore - 0.16)
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

    /// <summary>
    /// BM25 lexical retrieval over the current user's small in-memory chunk corpus.
    /// Scores are normalized to 0..1 so they can be fused with cosine similarity.
    /// </summary>
    internal static double[] Bm25Scores(string query, IReadOnlyList<string> documents)
    {
        var scores = new double[documents.Count];
        if (documents.Count == 0) return scores;

        var queryTerms = SearchTokens(query).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (queryTerms.Count == 0) return scores;

        var documentTerms = documents.Select(SearchTokens).ToList();
        var averageLength = Math.Max(1d, documentTerms.Average(terms => terms.Count));
        const double k1 = 1.35;
        const double b = 0.72;

        foreach (var term in queryTerms)
        {
            var documentFrequency = documentTerms.Count(terms => terms.Contains(term, StringComparer.OrdinalIgnoreCase));
            if (documentFrequency == 0) continue;
            var inverseDocumentFrequency = Math.Log(
                1 + ((documents.Count - documentFrequency + 0.5) / (documentFrequency + 0.5)));

            for (var index = 0; index < documentTerms.Count; index++)
            {
                var terms = documentTerms[index];
                var termFrequency = terms.Count(candidate => string.Equals(candidate, term, StringComparison.OrdinalIgnoreCase));
                if (termFrequency == 0) continue;
                var lengthNormalization = k1 * (1 - b + (b * terms.Count / averageLength));
                scores[index] += inverseDocumentFrequency *
                                 ((termFrequency * (k1 + 1)) / (termFrequency + lengthNormalization));
            }
        }

        var maximum = scores.DefaultIfEmpty(0).Max();
        if (maximum <= 0) return scores;
        for (var index = 0; index < scores.Length; index++)
        {
            // A single generic match such as «قیمت» must not make a document look
            // fully lexical when the other query concepts (for example a product
            // name) are absent. Preserve BM25 strength but gate it by term coverage.
            var matchedQueryTerms = queryTerms.Count(term =>
                documentTerms[index].Contains(term, StringComparer.OrdinalIgnoreCase));
            var queryCoverage = matchedQueryTerms / (double)queryTerms.Count;
            scores[index] = (scores[index] / maximum) * queryCoverage * queryCoverage;
        }
        return scores;
    }

    /// <summary>
    /// Weighted score fusion plus reciprocal-rank fusion. The rank component makes
    /// semantic and lexical retrieval vote independently instead of treating lexical
    /// overlap as only a small cosine bonus.
    /// </summary>
    internal static double FuseScores(
        double semanticScore,
        double lexicalScore,
        int semanticRank,
        int lexicalRank)
    {
        var reciprocalRank = SemanticRankWeight /
                             (ReciprocalRankConstant + Math.Max(1, semanticRank));
        if (lexicalRank > 0)
            reciprocalRank += LexicalRankWeight /
                              (ReciprocalRankConstant + lexicalRank);
        var maximumReciprocalRank = 1d / (ReciprocalRankConstant + 1);
        var normalizedRankScore = Math.Clamp(reciprocalRank / maximumReciprocalRank, 0, 1);

        return Math.Clamp(
            (Math.Max(0, semanticScore) * SemanticFusionWeight) +
            (Math.Clamp(lexicalScore, 0, 1) * LexicalFusionWeight) +
            (normalizedRankScore * RankFusionWeight),
            0,
            1);
    }

    internal static bool IsRelevant(double semanticScore, double lexicalScore, double threshold)
        => semanticScore >= threshold ||
           (lexicalScore >= 0.25 &&
            semanticScore + Math.Min(MaxLexicalRelevanceBoost, lexicalScore * MaxLexicalRelevanceBoost) >= threshold);

    internal static bool IsLexicalFallbackRelevant(double lexicalScore)
        => lexicalScore >= 0.55;

    private static RagAnswer BuildLexicalFallback(
        string query,
        IReadOnlyList<KnowledgeChunk> chunks,
        IReadOnlyList<double> bm25Scores,
        int topK)
    {
        var candidates = chunks
            .Select((chunk, index) =>
            {
                var fuzzyOverlap = LexicalSimilarity(query, chunk.Content);
                var lexicalScore = Math.Clamp((bm25Scores[index] * 0.70) + (fuzzyOverlap * 0.30), 0, 1);
                return new RagCandidate(index, chunk.Content, 0, lexicalScore, lexicalScore);
            })
            .OrderByDescending(candidate => candidate.LexicalScore)
            .Take(topK)
            .ToList();

        var best = candidates.FirstOrDefault();
        var found = best is not null && IsLexicalFallbackRelevant(best.LexicalScore);
        var context = found
            ? string.Join("\n---\n", candidates
                .Where(candidate => candidate.LexicalScore >= best!.LexicalScore - 0.16)
                .Select(candidate => candidate.Content))
            : "";
        var hits = candidates
            .Select(candidate => new RagHit(candidate.Content, candidate.LexicalScore))
            .ToList();
        return new RagAnswer(found, hits, context);
    }

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
        => SearchTokens(text).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static List<string> SearchTokens(string text)
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
            .Select(token => SynonymCanonical.GetValueOrDefault(token, token))
            .ToList();
    }

    private static IReadOnlyDictionary<string, string> BuildSynonymMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddSynonyms(map, "قیمت", "قیمت", "هزینه", "تعرفه", "مبلغ", "بها");
        AddSynonyms(map, "آدرس", "آدرس", "نشانی", "موقعیت", "مکان");
        AddSynonyms(map, "ساعت", "ساعت", "ساعات");
        AddSynonyms(map, "ثبت", "ثبت", "عضویت", "نامنویسی");
        AddSynonyms(map, "آنلاین", "آنلاین", "اینترنتی", "غیرحضوری");
        AddSynonyms(map, "خرید", "خرید", "سفارش", "تهیه");
        AddSynonyms(map, "لغو", "لغو", "کنسل", "ابطال");
        AddSynonyms(map, "تحویل", "تحویل", "ارسال");
        AddSynonyms(map, "پشتیبانی", "پشتیبانی", "کارشناس", "اپراتور");
        AddSynonyms(map, "تماس", "تماس", "تلفن");
        AddSynonyms(map, "خدمات", "خدمات", "سرویس");
        AddSynonyms(map, "گارانتی", "گارانتی", "ضمانت");
        AddSynonyms(map, "پرداخت", "پرداخت", "واریز");
        return map;
    }

    private static void AddSynonyms(
        IDictionary<string, string> map,
        string canonical,
        params string[] values)
    {
        foreach (var value in values) map[value] = canonical;
    }

    private sealed record EmbeddingEnvelope(string? Model, float[] Vector);
    private sealed record RagCandidate(
        int SourceIndex,
        string Content,
        double SemanticScore,
        double LexicalScore,
        double HybridScore);
}
