using ArkaCallCenter.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// پیاده‌سازی موقت: فقط پیامک را لاگ می‌کند. در فاز ۴ با SmsIrSender جایگزین می‌شود.
/// </summary>
public class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;
    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task<bool> SendAsync(string phoneNumber, string text, CancellationToken ct = default)
    {
        _logger.LogInformation("[SMS→{Phone}] {Text}", phoneNumber, text);
        return Task.FromResult(true);
    }
}
