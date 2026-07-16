namespace ArkaCallCenter.Realtime.Audio;

/// <summary>
/// پردازشِ پس از ضبط برای مکالمه‌ی ذخیره‌شده: کوتاه‌کردنِ فاصله‌های سکوتِ طولانی
/// (مثلاً بینِ سوالِ کاربر و پاسخِ AI) و حذفِ نویزِ زمینه در سکوت (noise gate)
/// تا صدا صاف‌تر و فشرده‌تر شنیده شود. ورودی/خروجی: SLIN (PCM16 LE، mono).
/// </summary>
public static class AudioPostProcess
{
    /// <param name="silenceThreshold">دامنه‌ی زیرِ این مقدار «سکوت» تلقی می‌شود.</param>
    /// <param name="maxSilenceMs">حداکثر سکوتی که در هر فاصله نگه داشته می‌شود.</param>
    public static byte[] CompressSilence(byte[] slin, int rate,
        int silenceThreshold = 600, int maxSilenceMs = 350)
    {
        if (slin.Length < 4) return slin;
        int maxSilenceSamples = Math.Max(1, rate * maxSilenceMs / 1000);
        int n = slin.Length / 2;
        var outBuf = new List<byte>(slin.Length);
        int silenceRun = 0;
        for (int i = 0; i < n; i++)
        {
            short s = (short)(slin[i * 2] | (slin[i * 2 + 1] << 8));
            if (Math.Abs((int)s) < silenceThreshold)
            {
                // سکوت: تا سقفِ maxSilenceSamples نگه دار (صفرشده برای حذف نویز)، بقیه را دور بریز.
                silenceRun++;
                if (silenceRun <= maxSilenceSamples)
                {
                    outBuf.Add(0);
                    outBuf.Add(0);
                }
            }
            else
            {
                silenceRun = 0;
                outBuf.Add(slin[i * 2]);
                outBuf.Add(slin[i * 2 + 1]);
            }
        }
        return outBuf.ToArray();
    }
}
