namespace ArkaCallCenter.Realtime.Audio;

/// <summary>
/// Builds a mono recording on the same 20 ms clock used to play audio to Asterisk.
/// Inbound caller audio is queued until the next clock tick and mixed with the exact
/// outbound frame that was played. This keeps both sides on one real-time timeline.
/// </summary>
public sealed class CallRecordingBuffer
{
    public const int FrameBytes = 320; // 20 ms of PCM16 mono at 8 kHz

    private readonly object _sync = new();
    private readonly LinkedList<byte[]> _inboundChunks = new();
    private readonly List<byte> _recording = new();
    private int _inboundHead;

    public void EnqueueInbound(byte[] pcm8k)
    {
        if (pcm8k.Length == 0) return;
        lock (_sync) _inboundChunks.AddLast(pcm8k);
    }

    /// <summary>Captures one real-time frame, mixing caller and played audio with saturation.</summary>
    public void CapturePlayedFrame(ReadOnlySpan<byte> outboundPcm8k)
    {
        Span<byte> inbound = stackalloc byte[FrameBytes];
        Span<byte> outbound = stackalloc byte[FrameBytes];
        Span<byte> mixed = stackalloc byte[FrameBytes];
        inbound.Clear();
        outbound.Clear();
        outboundPcm8k[..Math.Min(outboundPcm8k.Length, FrameBytes)].CopyTo(outbound);

        lock (_sync)
        {
            FillInboundFrame(inbound);
            AudioPostProcess.MixMonoPcm16(inbound, outbound, mixed);
            for (var i = 0; i < mixed.Length; i++) _recording.Add(mixed[i]);
        }
    }

    public byte[] ToArray()
    {
        lock (_sync) return _recording.ToArray();
    }

    private void FillInboundFrame(Span<byte> frame)
    {
        var filled = 0;
        while (filled < frame.Length && _inboundChunks.First is not null)
        {
            var chunk = _inboundChunks.First.Value;
            var available = chunk.Length - _inboundHead;
            var take = Math.Min(available, frame.Length - filled);
            chunk.AsSpan(_inboundHead, take).CopyTo(frame[filled..]);
            filled += take;
            _inboundHead += take;
            if (_inboundHead >= chunk.Length)
            {
                _inboundChunks.RemoveFirst();
                _inboundHead = 0;
            }
        }
    }
}
