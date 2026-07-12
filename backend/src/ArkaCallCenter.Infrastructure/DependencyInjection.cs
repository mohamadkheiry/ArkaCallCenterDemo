using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArkaCallCenter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");

        services.AddDbContext<ArkaDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUsageContext, UsageContext>();
        services.AddScoped<ITokenUsageTracker, TokenUsageTracker>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddHttpClient<ISmsSender, SmsIrSender>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<ISmsEventDispatcher, SmsEventDispatcher>();

        // فاز ۳: پایگاه دانش + RAG + moderation
        services.AddHttpClient<IOpenAiService, OpenAiService>(c => c.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<IRagService, RagService>();
        services.AddScoped<IFileTextExtractor, FileTextExtractor>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

        // فاز ۵: تخصیص داخلی + provisioning + ساخت تلفن هوشمند
        services.AddScoped<IExtensionAllocator, ExtensionAllocator>();
        services.AddScoped<IAsteriskProvisioningService, AsteriskProvisioningService>();
        services.AddScoped<ISmartPhoneService, SmartPhoneService>();

        return services;
    }
}
