using ArkaCallCenter.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>
/// ساخت داخلی روی ایزابل (Asterisk) از طریق SSH با نوشتن یک بلوک PJSIP در
/// فایل include سفارشی و reload. اگر اطلاعات SSH پیکربندی نشده باشد، عملیات
/// شبیه‌سازی می‌شود (حالت توسعه) تا جریان کامل قابل تست باشد.
///
/// نکته: جزئیات دقیق ممکن است بسته به پیکربندی ایزابل (FreePBX/PJSIP/chan_sip)
/// نیاز به تنظیم داشته باشد؛ به docs/TELEPHONY.md مراجعه شود.
/// </summary>
public class AsteriskProvisioningService : IAsteriskProvisioningService
{
    private const string CustomConf = "/etc/asterisk/pjsip_custom.conf";
    private const string StasisContext = "arka-ai"; // context ورودی برای پاسخ‌گویی AI (فاز ۶)

    private readonly IConfiguration _config;
    private readonly ILogger<AsteriskProvisioningService> _logger;

    public AsteriskProvisioningService(IConfiguration config, ILogger<AsteriskProvisioningService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private (string host, string user, string pass)? Creds()
    {
        var host = _config["Asterisk:Host"] ?? _config["ASTERISK_HOST"];
        var user = _config["Asterisk:SshUser"] ?? _config["ASTERISK_SSH_USER"];
        var pass = _config["Asterisk:SshPassword"] ?? _config["ASTERISK_SSH_PASSWORD"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            return null;
        return (host!, user!, pass!);
    }

    public Task<ProvisionResult> ProvisionExtensionAsync(int extension, string secret, CancellationToken ct = default)
    {
        var creds = Creds();
        if (creds is null)
        {
            _logger.LogWarning("Asterisk SSH not configured; simulating provisioning of extension {Ext}", extension);
            return Task.FromResult(new ProvisionResult(true, null));
        }

        var block = BuildPjsipBlock(extension, secret);
        // نوشتن بلوک در فایل include و reload
        var cmd =
            $"cat >> {CustomConf} <<'ARKA_EOF'\n{block}\nARKA_EOF\n" +
            "asterisk -rx 'pjsip reload' && asterisk -rx 'dialplan reload'";

        try
        {
            var (host, user, pass) = creds.Value;
            using var client = new SshClient(host, user, pass);
            client.Connect();
            var result = client.RunCommand(cmd);
            client.Disconnect();
            if (result.ExitStatus != 0)
            {
                _logger.LogError("Provisioning failed for {Ext}: {Err}", extension, result.Error);
                return Task.FromResult(new ProvisionResult(false, "خطا در ساخت داخلی روی سرور ایزابل."));
            }
            return Task.FromResult(new ProvisionResult(true, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH provisioning error for {Ext}", extension);
            return Task.FromResult(new ProvisionResult(false, "اتصال به سرور ایزابل ممکن نشد."));
        }
    }

    public Task RemoveExtensionAsync(int extension, CancellationToken ct = default)
    {
        // TODO: حذف بلوک مربوط به داخلی از فایل و reload (نیازمند parse فایل).
        _logger.LogInformation("RemoveExtension {Ext} requested (not yet implemented).", extension);
        return Task.CompletedTask;
    }

    private static string BuildPjsipBlock(int ext, string secret) => $"""
        ;=== ARKA extension {ext} ===
        [{ext}]
        type=endpoint
        transport=transport-udp
        context={StasisContext}
        disallow=all
        allow=ulaw
        allow=alaw
        auth={ext}-auth
        aors={ext}

        [{ext}-auth]
        type=auth
        auth_type=userpass
        username={ext}
        password={secret}

        [{ext}]
        type=aor
        max_contacts=1
        """;
}
