namespace ArkaCallCenter.Core.Abstractions;

/// <summary>
/// برقراری تماس صوتی و خواندنِ یک متن با صدای گنجی (piper) روی سرور Isabel.
/// از طریق سرویسِ HTTP آرکا-ویس (arka-voice) روی PBX انجام می‌شود.
/// </summary>
public interface IVoiceCaller
{
    /// <param name="rawText">اگر true باشد، متن دقیقاً همان‌طور به TTS می‌رود و قاعده‌ی
    /// «ویرگول بین کلمات» اعمال نمی‌شود (برای کنترلِ دقیقِ فرمت مثل ارقامِ OTP).</param>
    Task<bool> CallAndSpeakAsync(string phoneNumber, string text, bool rawText = false, CancellationToken ct = default);
}
