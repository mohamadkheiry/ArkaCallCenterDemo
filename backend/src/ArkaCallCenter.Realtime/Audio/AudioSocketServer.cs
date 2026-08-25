using System.Net;
using System.Net.Sockets;
using ArkaCallCenter.Realtime.Call;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArkaCallCenter.Realtime.Audio;

/// <summary>
/// سرور TCP پروتکل AudioSocket. Asterisk (اپلیکیشن AudioSocket در dialplan) به آن
/// وصل می‌شود و هر اتصال با مسیر Whisper → پاک‌سازی → تطبیق سؤال → صوت ثابت پاسخ داده می‌شود.
/// </summary>
public class AudioSocketServer : BackgroundService
{
    private readonly RealtimeOptions _options;
    private readonly QaCallHandler _handler;
    private readonly ILogger<AudioSocketServer> _logger;

    public AudioSocketServer(IOptions<RealtimeOptions> options, QaCallHandler handler, ILogger<AudioSocketServer> logger)
    {
        _options = options.Value;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Parse(_options.AudioSocketHost), _options.AudioSocketPort);
        listener.Start();
        _logger.LogInformation("AudioSocket server listening on {Host}:{Port}", _options.AudioSocketHost, _options.AudioSocketPort);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _logger.LogInformation("Incoming call connection from {Remote}", client.Client.RemoteEndPoint);
                _ = Task.Run(async () =>
                {
                    try { await _handler.HandleAsync(client, stoppingToken); }
                    catch (Exception ex) { _logger.LogError(ex, "Unhandled error in call handler"); }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            listener.Stop();
        }
    }
}
