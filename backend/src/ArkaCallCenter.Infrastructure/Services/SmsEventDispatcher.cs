using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// موتور رویداد→پیامک: قالب و گیرندگانِ تنظیم‌شده‌ی هر رویداد را می‌خواند،
/// متغیرها را جای‌گذاری و از طریق ISmsSender ارسال می‌کند.
/// </summary>
public class SmsEventDispatcher : ISmsEventDispatcher
{
    private readonly ArkaDbContext _db;
    private readonly ISmsSender _sms;
    private readonly ILogger<SmsEventDispatcher> _logger;

    public SmsEventDispatcher(ArkaDbContext db, ISmsSender sms, ILogger<SmsEventDispatcher> logger)
    {
        _db = db;
        _sms = sms;
        _logger = logger;
    }

    public async Task DispatchAsync(SmsEventType eventType, IDictionary<string, string> variables,
        string? relatedUserPhone = null, CancellationToken ct = default)
    {
        var template = await _db.SmsTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventType == eventType, ct);
        if (template is null || !template.Enabled)
            return;

        var body = Render(template.Body, variables);

        var recipients = await _db.SmsEventRecipients.AsNoTracking()
            .Where(r => r.EventType == eventType)
            .ToListAsync(ct);

        // هر دو مقصد مستقل‌اند: هم می‌توان به خودِ کاربر ارسال کرد و هم به لیست شماره‌های ثابت
        // (یا فقط یکی، یا هیچ‌کدام). ترکیب همه‌ی رکوردهای این رویداد ساخته می‌شود.
        var numbers = new HashSet<string>();
        foreach (var r in recipients)
        {
            if (r.UseUserOwnNumber && !string.IsNullOrWhiteSpace(relatedUserPhone))
                numbers.Add(relatedUserPhone!);

            if (!string.IsNullOrWhiteSpace(r.PhoneNumber))
            {
                // چند شماره‌ی جداشده با , ، یا خط جدید
                foreach (var n in r.PhoneNumber.Split(new[] { ',', '،', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    numbers.Add(n);
            }
        }

        foreach (var number in numbers)
        {
            try { await _sms.SendAsync(number, body, ct); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to send SMS for {Event} to {Number}", eventType, number); }
        }
    }

    private static string Render(string template, IDictionary<string, string> vars)
    {
        foreach (var kv in vars)
            template = template.Replace("{" + kv.Key + "}", kv.Value);
        return template;
    }
}
