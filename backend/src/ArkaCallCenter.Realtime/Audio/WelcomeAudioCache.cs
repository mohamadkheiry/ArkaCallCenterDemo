using System.Collections.Concurrent;
using ArkaCallCenter.Infrastructure.Audio;

namespace ArkaCallCenter.Realtime.Audio;

/// <summary>
/// Keeps ready-to-play welcome audio in memory so a call can start speaking before
/// database queries and the OpenAI WebSocket handshake complete.
/// </summary>
public sealed class WelcomeAudioCache
{
    private sealed record Entry(string Path, DateTime LastWriteUtc, byte[] Slin8k);

    private readonly ConcurrentDictionary<int, Entry> _entries = new();
    private readonly ILogger<WelcomeAudioCache> _logger;

    public WelcomeAudioCache(ILogger<WelcomeAudioCache> logger) => _logger = logger;

    public bool TryGet(int extension, out byte[] audio)
    {
        audio = Array.Empty<byte>();
        if (!_entries.TryGetValue(extension, out var entry)) return false;

        try
        {
            if (!File.Exists(entry.Path))
            {
                _entries.TryRemove(extension, out _);
                return false;
            }

            var lastWrite = File.GetLastWriteTimeUtc(entry.Path);
            if (lastWrite != entry.LastWriteUtc)
            {
                entry = LoadEntry(entry.Path);
                _entries[extension] = entry;
            }

            audio = entry.Slin8k;
            return audio.Length > 0;
        }
        catch (Exception ex)
        {
            _entries.TryRemove(extension, out _);
            _logger.LogWarning(ex, "Could not refresh welcome cache for ext {Ext}.", extension);
            return false;
        }
    }

    public bool TrySet(int extension, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
            return false;

        try
        {
            _entries[extension] = LoadEntry(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not populate welcome cache for ext {Ext}.", extension);
            return false;
        }
    }

    private static Entry LoadEntry(string path)
    {
        var wav = File.ReadAllBytes(path);
        var slin = AudioPostProcess.CompressSilence(
            AudioConvert.WavToSlin8k(wav),
            AudioConvert.TelephonyRate,
            maxSilenceMs: 120);
        return new Entry(path, File.GetLastWriteTimeUtc(path), slin);
    }
}
