namespace ArkaCallCenter.Realtime.Audio;

/// <summary>
/// تبدیل نرخ نمونه‌برداری بین ۸kHz (AudioSocket/تلفن) و ۲۴kHz (OpenAI realtime PCM16)
/// با درون‌یابی خطی ساده. ورودی/خروجی: PCM 16-bit signed little-endian، mono.
/// </summary>
public static class AudioResampler
{
    public const int TelephonyRate = 8000;
    public const int OpenAiRate = 24000;

    public static byte[] Upsample8kTo24k(ReadOnlySpan<byte> pcm8k) => Resample(pcm8k, TelephonyRate, OpenAiRate);
    public static byte[] Downsample24kTo8k(ReadOnlySpan<byte> pcm24k) => Resample(pcm24k, OpenAiRate, TelephonyRate);

    private static byte[] Resample(ReadOnlySpan<byte> input, int inRate, int outRate)
    {
        var inSamples = input.Length / 2;
        if (inSamples == 0) return Array.Empty<byte>();

        var src = new short[inSamples];
        for (var i = 0; i < inSamples; i++)
            src[i] = (short)(input[i * 2] | (input[i * 2 + 1] << 8));

        var outSamples = (int)((long)inSamples * outRate / inRate);
        var dst = new byte[outSamples * 2];
        var ratio = (double)(inSamples - 1) / Math.Max(1, outSamples - 1);

        for (var i = 0; i < outSamples; i++)
        {
            var pos = i * ratio;
            var idx = (int)pos;
            var frac = pos - idx;
            var s0 = src[Math.Min(idx, inSamples - 1)];
            var s1 = src[Math.Min(idx + 1, inSamples - 1)];
            var val = (short)(s0 + (s1 - s0) * frac);
            dst[i * 2] = (byte)(val & 0xFF);
            dst[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        return dst;
    }
}
