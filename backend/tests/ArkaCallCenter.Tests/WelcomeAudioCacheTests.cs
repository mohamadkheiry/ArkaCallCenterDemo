using ArkaCallCenter.Infrastructure.Audio;
using ArkaCallCenter.Realtime.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ArkaCallCenter.Tests;

public class WelcomeAudioCacheTests
{
    [Fact]
    public void Expected_versioned_path_replaces_the_cached_welcome_immediately()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"arka-welcome-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var firstPath = Path.Combine(directory, "welcome_first.wav");
            var secondPath = Path.Combine(directory, "welcome_second.wav");
            File.WriteAllBytes(firstPath, AudioConvert.WriteWav(
                Enumerable.Repeat((short)1200, 1600).ToArray(),
                AudioConvert.TelephonyRate));
            File.WriteAllBytes(secondPath, AudioConvert.WriteWav(
                Enumerable.Repeat((short)-2400, 1600).ToArray(),
                AudioConvert.TelephonyRate));

            var cache = new WelcomeAudioCache(NullLogger<WelcomeAudioCache>.Instance);
            Assert.True(cache.TrySet(4321, firstPath));
            Assert.True(cache.TryGet(4321, firstPath, out var first));

            Assert.True(cache.TryGet(4321, secondPath, out var second));

            Assert.NotEmpty(first);
            Assert.NotEmpty(second);
            Assert.False(first.SequenceEqual(second));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
