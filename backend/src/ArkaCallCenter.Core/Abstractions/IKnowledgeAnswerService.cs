using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Abstractions;

public record KnowledgeAnswerItem(
    int Id,
    string Question,
    string Answer,
    int SortOrder,
    KnowledgeAnswerAudioStatus AudioStatus,
    string? AudioError,
    DateTime UpdatedAt);

public record KnowledgeAnswerPage(int Total, IReadOnlyList<KnowledgeAnswerItem> Items);
public record KnowledgeAnswerResult(bool Ok, string? Error, KnowledgeAnswerItem? Item);
public record KnowledgeFallbackResult(
    bool Ok,
    string? Error,
    string? Text,
    bool AudioReady,
    DateTime? UpdatedAt = null);
public record KnowledgeAnswerMatch(
    bool Found,
    int? Id,
    string? Question,
    string? Answer,
    string? AudioPath,
    double Score);

public interface IKnowledgeAnswerService
{
    Task<KnowledgeAnswerPage> ListAsync(int userId, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<KnowledgeAnswerResult> AddAsync(int userId, string question, string answer, CancellationToken ct = default);
    Task<KnowledgeAnswerResult> UpdateAsync(int userId, int id, string question, string answer, CancellationToken ct = default);
    Task DeleteAsync(int userId, int id, CancellationToken ct = default);
    Task<KnowledgeAnswerResult> RegenerateAudioAsync(int userId, int id, CancellationToken ct = default);
    Task<KnowledgeFallbackResult> GetFallbackAsync(int userId, CancellationToken ct = default);
    Task<KnowledgeFallbackResult> SetFallbackAsync(int userId, string text, CancellationToken ct = default);
    Task<KnowledgeAnswerMatch> MatchAsync(int userId, string question, CancellationToken ct = default);
}
