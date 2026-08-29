using ArkaCallCenter.Realtime.Audio;
using Xunit;

namespace ArkaCallCenter.Tests;

public class AudioPostProcessTests
{
    [Fact]
    public void Low_level_line_noise_is_classified_as_silence()
    {
        var frame = PcmFrame(60);

        Assert.True(AudioPostProcess.IsSilentFrame(frame, 140));
    }

    [Fact]
    public void Telephone_speech_level_is_not_classified_as_silence()
    {
        var frame = PcmFrame(1200);

        Assert.False(AudioPostProcess.IsSilentFrame(frame, 140));
    }

    private static byte[] PcmFrame(short amplitude)
    {
        var frame = new byte[320];
        for (var i = 0; i < frame.Length; i += 2)
        {
            var sample = (short)((i / 2 % 2 == 0) ? amplitude : -amplitude);
            frame[i] = (byte)(sample & 0xff);
            frame[i + 1] = (byte)((sample >> 8) & 0xff);
        }
        return frame;
    }
}
