using ArkaCallCenter.Infrastructure;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Realtime;
using ArkaCallCenter.Realtime.Audio;
using ArkaCallCenter.Realtime.Call;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// دسترسی به DbContext/RAG/Settings از لایه‌ی Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<RealtimeOptions>(builder.Configuration.GetSection("Realtime"));
builder.Services.AddSingleton<WelcomeAudioCache>();
builder.Services.AddSingleton<CallHandler>();
builder.Services.AddHostedService<AudioSocketServer>();

var host = builder.Build();

// گرم‌کردن EF Core پیش از پذیرش تماس‌ها؛ در غیر این صورت اولین تماس دچار «کولد استارت»
// (~۲ ثانیه تأخیر روی اولین کوئری) می‌شود که می‌تواند شروع پخش صدا را عقب بیندازد.
try
{
    using var warmScope = host.Services.CreateScope();
    var warmDb = warmScope.ServiceProvider.GetRequiredService<ArkaDbContext>();
    var welcomeCache = host.Services.GetRequiredService<WelcomeAudioCache>();
    var activeWelcomes = await warmDb.SmartPhones.AsNoTracking()
        .Where(s => s.Extension != null && s.Status == ArkaCallCenter.Core.Enums.SmartPhoneStatus.Active)
        .Select(s => new { Extension = s.Extension!.Value, s.WelcomeAudioPath })
        .ToListAsync();
    foreach (var item in activeWelcomes)
        welcomeCache.TrySet(item.Extension, item.WelcomeAudioPath);

    // Compile and execute the same entity shape used by CallHandler so the first
    // real caller does not pay EF query-compilation and relationship materialization costs.
    if (activeWelcomes.FirstOrDefault() is { } firstActive)
    {
        _ = await warmDb.SmartPhones.AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Extension == firstActive.Extension);
    }
}
catch { /* اگر دیتابیس هنوز آماده نیست، بی‌خیال؛ تماس اول کمی کندتر خواهد بود */ }

host.Run();
