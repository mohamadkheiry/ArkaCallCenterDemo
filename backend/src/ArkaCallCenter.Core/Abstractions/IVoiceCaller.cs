namespace ArkaCallCenter.Core.Abstractions;

/// <summary>
/// برقراری تماس صوتی و خواندنِ یک متن با صدای گنجی (piper) روی سرور Isabel.
/// از طریق سرویسِ HTTP آرکا-ویس (arka-voice) روی PBX انجام می‌شود.
/// </summary>
public interface IVoiceCaller
{
    Task<bool> CallAndSpeakAsync(string phoneNumber, string text, CancellationToken ct = default);
}
