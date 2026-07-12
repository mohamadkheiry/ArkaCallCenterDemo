using System.Buffers.Binary;

namespace ArkaCallCenter.Realtime.Audio;

/// <summary>
/// پروتکل AudioSocket استریسک: هر پیام = [۱ بایت نوع][۲ بایت طول big-endian][payload].
/// صدا SLIN است: PCM 16-bit signed little-endian، ۸kHz، mono.
/// </summary>
public static class AudioSocketProtocol
{
    public const byte KindHangup = 0x00;
    public const byte KindId = 0x01;   // payload: 16 بایت UUID
    public const byte KindError = 0xff;
    public const byte KindAudio = 0x10; // payload: SLIN

    public readonly record struct Frame(byte Kind, byte[] Payload);

    public static async Task<Frame?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[3];
        if (!await ReadExactAsync(stream, header, ct)) return null;
        var kind = header[0];
        var len = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1, 2));
        var payload = new byte[len];
        if (len > 0 && !await ReadExactAsync(stream, payload, ct)) return null;
        return new Frame(kind, payload);
    }

    public static async Task WriteAudioAsync(Stream stream, byte[] slin8k, CancellationToken ct)
    {
        // در قطعات ~۲۰ms (۳۲۰ بایت) ارسال می‌شود.
        const int chunk = 320;
        for (var off = 0; off < slin8k.Length; off += chunk)
        {
            var size = Math.Min(chunk, slin8k.Length - off);
            var frame = new byte[3 + size];
            frame[0] = KindAudio;
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1, 2), (ushort)size);
            Array.Copy(slin8k, off, frame, 3, size);
            await stream.WriteAsync(frame, ct);
        }
    }

    /// <summary>
    /// استخراج شماره داخلی از UUID. طبق قرارداد، dialplan آخرین بخش UUID را برابر
    /// شماره‌ی داخلیِ صفرپرشده (۱۲ رقم اعشاری) قرار می‌دهد؛ مثلاً
    /// 00000000-0000-0000-0000-000000001005 → داخلی ۱۰۰۵.
    /// </summary>
    public static int? ParseExtension(byte[] uuid16)
    {
        if (uuid16.Length < 16) return null;
        var last6 = uuid16.AsSpan(10, 6);
        var hex = Convert.ToHexString(last6); // ۱۲ کاراکتر
        return int.TryParse(hex, out var ext) ? ext : null;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }
}
