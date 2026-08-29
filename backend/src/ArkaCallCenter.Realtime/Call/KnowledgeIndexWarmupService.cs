using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Realtime.Call;

/// <summary>
/// بعد از بالا آمدن AudioSocket، ایندکس‌های قدیمی RAG را در پس‌زمینه با مدل فعلی
/// هماهنگ می‌کند تا اولین تماس هزینه‌ی بازسازی ایندکس را نپردازد.
/// </summary>
public sealed class KnowledgeIndexWarmupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KnowledgeIndexWarmupService> _logger;

    public KnowledgeIndexWarmupService(
        IServiceScopeFactory scopeFactory,
        ILogger<KnowledgeIndexWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // اجرای سرویس تماس را برای عملیات نگهداری RAG معطل نکن.
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArkaDbContext>();
            var rag = scope.ServiceProvider.GetRequiredService<IRagService>();
            var userIds = await db.KnowledgeBases
                .AsNoTracking()
                .Where(kb => kb.RawText != null && kb.RawText != "")
                .Select(kb => kb.UserId)
                .ToListAsync(stoppingToken);

            foreach (var userId in userIds)
            {
                try
                {
                    await rag.EnsureIndexAsync(userId, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Could not warm RAG index for user {UserId}.", userId);
                }
            }

            _logger.LogInformation("RAG index warmup completed for {Count} knowledge bases.", userIds.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG index warmup could not be completed.");
        }
    }
}
