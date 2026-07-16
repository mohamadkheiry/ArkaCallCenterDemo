using System.Buffers.Binary;

namespace ArkaCallCenter.Infrastructure.Audio;

/// <summary>
/// ابزارهای صوتی: تبدیل PCM اوپن‌ای‌آی (۲۴kHz) به WAV ۸kHz برای پخش در dialplan ایزابل،
/// و تبدیل WAV آپلودی به SLIN ۸kHz خام برای استریم موسیقی انتظار در worker.
/// خروجی همیشه mono 16-bit است.
/// </summary>
public static class AudioConvert
{
    public const int TelephonyRate = 8000;

    /// <summary>PCM16 با نرخ دلخواه → WAV ۸kHz mono (برای Playback ایزابل به‌صورت فرمت «wav»).</summary>
    public static byte[] PcmToWav8k(byte[] pcm, int inRate)
    {
        var samples = BytesToShorts(pcm);
        var resampled = Resample(samples, inRate, TelephonyRate);
        return WriteWav(resampled, TelephonyRate);
    }

    /// <summary>فایل WAV (هر نرخ/کانال) → SLIN خام ۸kHz mono (PCM16 LE).</summary>
    public static byte[] WavToSlin8k(byte[] wav)
    {
        var (samples, rate) = ParseWav(wav);
        var resampled = Resample(samples, rate, TelephonyRate);
        return ShortsToBytes(resampled);
    }

    /// <summary>فایل WAV (هر نرخ/کانال) → فایل WAV ۸kHz mono ۱۶بیت (فرمت «wav» ایزابل).</summary>
    public static byte[] WavToWav8k(byte[] wav)
    {
        var (samples, rate) = ParseWav(wav);
        var resampled = Resample(samples, rate, TelephonyRate);
        return WriteWav(resampled, TelephonyRate);
    }

    // ---------- WAV ----------
    public static byte[] WriteWav(short[] samples, int rate)
    {
        var dataLen = samples.Length * 2;
        var buf = new byte[44 + dataLen];
        var s = buf.AsSpan();
        // RIFF
        "RIFF"u8.CopyTo(s);
        BinaryPrimitives.WriteInt32LittleEndian(s.Slice(4), 36 + dataLen);
        "WAVE"u8.CopyTo(s.Slice(8));
        // fmt
        "fmt "u8.CopyTo(s.Slice(12));
        BinaryPrimitives.WriteInt32LittleEndian(s.Slice(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(s.Slice(20), 1);   // PCM
        BinaryPrimitives.WriteInt16LittleEndian(s.Slice(22), 1);   // mono
        BinaryPrimitives.WriteInt32LittleEndian(s.Slice(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(s.Slice(28), rate * 2); // byte rate
        BinaryPrimitives.WriteInt16LittleEndian(s.Slice(32), 2);   // block align
        BinaryPrimitives.WriteInt16LittleEndian(s.Slice(34), 16);  // bits
        // data
        "data"u8.CopyTo(s.Slice(36));
        BinaryPrimitives.WriteInt32LittleEndian(s.Slice(40), dataLen);
        for (var i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(s.Slice(44 + i * 2), samples[i]);
        return buf;
    }

    private static (short[] samples, int rate) ParseWav(byte[] wav)
    {
        var s = wav.AsSpan();
        if (wav.Length < 44 || !s.Slice(0, 4).SequenceEqual("RIFF"u8) || !s.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("فایل WAV معتبر نیست.");

        int channels = 1, rate = TelephonyRate, bits = 16;
        int pos = 12, dataStart = -1, dataLen = 0;
        while (pos + 8 <= wav.Length)
        {
            var id = s.Slice(pos, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(s.Slice(pos + 4, 4));
            var body = pos + 8;
            // بدنه‌ی fmt حداقل ۱۶ بایت است؛ اگر فایل بریده باشد نباید از بافر بیرون بخوانیم.
            if (id.SequenceEqual("fmt "u8) && body + 16 <= wav.Length)
            {
                channels = BinaryPrimitives.ReadInt16LittleEndian(s.Slice(body + 2, 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(s.Slice(body + 4, 4));
                bits = BinaryPrimitives.ReadInt16LittleEndian(s.Slice(body + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                dataStart = body;
                dataLen = Math.Min(size, wav.Length - body);
            }
            pos = body + size + (size % 2); // chunkها padded به مضرب ۲
        }
        if (dataStart < 0) throw new InvalidDataException("بخش data در WAV یافت نشد.");
        if (bits != 16) throw new NotSupportedException("فقط WAV با ۱۶ بیت پشتیبانی می‌شود.");

        var totalSamples = dataLen / 2;
        var interleaved = new short[totalSamples];
        for (var i = 0; i < totalSamples; i++)
            interleaved[i] = BinaryPrimitives.ReadInt16LittleEndian(s.Slice(dataStart + i * 2, 2));

        if (channels <= 1) return (interleaved, rate);

        // ترکیب کانال‌ها به mono (میانگین)
        var frames = totalSamples / channels;
        var mono = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            long sum = 0;
            for (var c = 0; c < channels; c++) sum += interleaved[i * channels + c];
            mono[i] = (short)(sum / channels);
        }
        return (mono, rate);
    }

    // ---------- resample (linear) ----------
    public static short[] Resample(short[] src, int inRate, int outRate)
    {
        if (src.Length == 0 || inRate == outRate) return src;
        var outLen = (int)((long)src.Length * outRate / inRate);
        if (outLen <= 0) return Array.Empty<short>();
        var dst = new short[outLen];
        var ratio = (double)(src.Length - 1) / Math.Max(1, outLen - 1);
        for (var i = 0; i < outLen; i++)
        {
            var pos = i * ratio;
            var idx = (int)pos;
            var frac = pos - idx;
            var a = src[Math.Min(idx, src.Length - 1)];
            var b = src[Math.Min(idx + 1, src.Length - 1)];
            dst[i] = (short)(a + (b - a) * frac);
        }
        return dst;
    }

    private static short[] BytesToShorts(byte[] pcm)
    {
        var n = pcm.Length / 2;
        var s = new short[n];
        for (var i = 0; i < n; i++) s[i] = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
        return s;
    }

    private static byte[] ShortsToBytes(short[] samples)
    {
        var b = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            b[i * 2] = (byte)(samples[i] & 0xFF);
            b[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }
        return b;
    }
}
