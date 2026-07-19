namespace ArkaCallCenter.Realtime.Audio;

/// <summary>
/// پردازشِ پس از ضبط برای مکالمه‌ی ذخیره‌شده: کوتاه‌کردنِ فاصله‌های سکوتِ طولانی
/// (مثلاً بینِ سوالِ کاربر و پاسخِ AI) و حذفِ نویزِ زمینه در سکوت (noise gate)
/// تا صدا صاف‌تر و فشرده‌تر شنیده شود. ورودی/خروجی: SLIN (PCM16 LE، mono).
/// </summary>
public static class AudioPostProcess
{
    /// <param name="silenceThreshold">RMS کمتر از این مقدار، با شرط پایین‌بودن peak، سکوت تلقی می‌شود.</param>
    /// <param name="maxSilenceMs">حداکثر سکوتی که در هر فاصله نگه داشته می‌شود.</param>
    public static byte[] CompressSilence(byte[] slin, int rate,
        int silenceThreshold = 120, int maxSilenceMs = 280, int frameMs = 20)
    {
        if (slin.Length < 4) return slin;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameMs);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSilenceMs);

        var frameBytes = Math.Max(2, rate * frameMs / 1000 * 2);
        var frameCount = (slin.Length + frameBytes - 1) / frameBytes;
        var silent = new bool[frameCount];
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var start = frameIndex * frameBytes;
            var length = Math.Min(frameBytes, slin.Length - start);
            silent[frameIndex] = IsSilentFrame(slin.AsSpan(start, length), silenceThreshold);
        }

        var maxSilentFrames = Math.Max(1, (maxSilenceMs + frameMs - 1) / frameMs);
        using var output = new MemoryStream(slin.Length);
        var index = 0;
        while (index < frameCount)
        {
            var runStart = index;
            var isSilent = silent[index];
            while (index < frameCount && silent[index] == isSilent) index++;
            var runFrames = index - runStart;

            if (!isSilent || runFrames <= maxSilentFrames)
            {
                WriteFrames(output, slin, runStart, runFrames, frameBytes);
                continue;
            }

            // Preserve both edges of an internal pause. Keeping whole frames instead of
            // gating individual samples avoids cutting low-volume consonants and word tails.
            if (runStart == 0)
            {
                WriteFrames(output, slin, index - maxSilentFrames, maxSilentFrames, frameBytes);
            }
            else if (index == frameCount)
            {
                WriteFrames(output, slin, runStart, maxSilentFrames, frameBytes);
            }
            else
            {
                var leadingFrames = maxSilentFrames / 2;
                var trailingFrames = maxSilentFrames - leadingFrames;
                WriteFrames(output, slin, runStart, leadingFrames, frameBytes);
                WriteFrames(output, slin, index - trailingFrames, trailingFrames, frameBytes);
            }
        }

        return output.ToArray();
    }

    /// <summary>Mixes two PCM16 little-endian mono buffers and saturates instead of wrapping.</summary>
    public static void MixMonoPcm16(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> destination)
    {
        var byteCount = Math.Min(destination.Length, Math.Min(first.Length, second.Length)) & ~1;
        for (var i = 0; i < byteCount; i += 2)
        {
            var a = (short)(first[i] | first[i + 1] << 8);
            var b = (short)(second[i] | second[i + 1] << 8);
            var mixed = Math.Clamp((int)a + b, short.MinValue, short.MaxValue);
            destination[i] = (byte)(mixed & 0xff);
            destination[i + 1] = (byte)((mixed >> 8) & 0xff);
        }
    }

    private static bool IsSilentFrame(ReadOnlySpan<byte> pcm, int rmsThreshold)
    {
        var sampleCount = pcm.Length / 2;
        if (sampleCount == 0) return true;

        long squareSum = 0;
        var peak = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (short)(pcm[i * 2] | pcm[i * 2 + 1] << 8);
            var absolute = Math.Abs((int)sample);
            peak = Math.Max(peak, absolute);
            squareSum += (long)sample * sample;
        }

        var rms = Math.Sqrt(squareSum / (double)sampleCount);
        return rms < rmsThreshold && peak < rmsThreshold * 5;
    }

    private static void WriteFrames(Stream output, byte[] source, int startFrame, int count, int frameBytes)
    {
        if (count <= 0) return;
        var offset = startFrame * frameBytes;
        var length = Math.Min(count * frameBytes, source.Length - offset);
        if (length > 0) output.Write(source, offset, length);
    }
}
