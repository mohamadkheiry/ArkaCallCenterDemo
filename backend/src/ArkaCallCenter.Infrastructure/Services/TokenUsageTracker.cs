using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

public class TokenUsageTracker : ITokenUsageTracker
{
    private readonly ArkaDbContext _db;
    private readonly IUsageContext _usage;
    private readonly ILogger<TokenUsageTracker> _logger;

    public TokenUsageTracker(ArkaDbContext db, IUsageContext usage, ILogger<TokenUsageTracker> logger)
    {
        _db = db;
        _usage = usage;
        _logger = logger;
    }

    public async Task RecordAsync(string operation, string model, string apiKeyFingerprint,
        int promptTokens, int completionTokens, int totalTokens, CancellationToken ct = default)
    {
        try
        {
            // شماره موبایل: از context یا از روی UserId
            var phone = _usage.PhoneNumber;
            if (phone is null && _usage.UserId is int uid)
                phone = await _db.Users.Where(u => u.Id == uid).Select(u => u.PhoneNumber).FirstOrDefaultAsync(ct);

            _db.TokenUsages.Add(new TokenUsage
            {
                ApiKeyFingerprint = apiKeyFingerprint,
                UserId = _usage.UserId,
                PhoneNumber = phone,
                Model = model,
                Operation = operation,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // ثبت مصرف نباید جریان اصلی را بشکند.
            _logger.LogWarning(ex, "Failed to record token usage ({Op}/{Model})", operation, model);
        }
    }
}
