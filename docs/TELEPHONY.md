# ادغام تلفنی (Isabel / Asterisk) — طراحی

> فاز ۶. این سند طراحی است؛ پیاده‌سازی در `backend/src/ArkaCallCenter.Realtime` و اسکریپت‌های `telephony/`.

## سرور
- Isabel (مبتنی بر Asterisk) روی `192.168.10.101`.
- دسترسی: SSH (`root`) — **اسرار در `.env`، نه در گیت**.

## Provisioning داخلی (فاز ۵)
دو گزینه:
1. **AMI/CLI روی SSH:** نوشتن پیکربندی PJSIP/SIP برای داخلی جدید و `dialplan reload`.
2. **پیکربندی فایل‌محور:** درج در `pjsip_custom.conf` + reload.

`ExtensionAllocator` تضمین می‌کند عدد داخلی در بازه‌ی ۱۰۰۰–۹۹۹۹ و بدون تکرار باشد (بررسی هم در DB و هم در Asterisk).

## پاسخ‌گویی هوشمند (فاز ۶)
1. dialplan تماسِ داخلی کاربر را وارد Stasis app به نام `arka-ai` می‌کند.
2. Worker از طریق **ARI** کانال را `answer` می‌کند و یک `externalMedia` (یا AudioSocket) با فرمت `slin16` می‌سازد.
3. پلی «وویس خوش‌آمد» (فایل از پیش‌ساخته‌ی کاربر).
4. صدای caller → استریم به WebSocket `gpt-realtime`؛ instructions شامل context بازیابی‌شده از RAG.
5. صدای خروجی realtime → برگشت به bridge → پلی برای caller.
6. اگر RAG زیر آستانه بود → پلی فایل fallback از پیش‌ساخته و عدم فراخوانی realtime (صرفه‌جویی توکن).
7. شمارش زمان مکالمه؛ در سقف دقیقه، پیام و قطع.

## نکات فرمت صوت
- `gpt-realtime` معمولاً PCM16 (۲۴kHz یا ۱۶kHz) می‌پذیرد؛ Asterisk `slin16`=16kHz. در صورت g711 (۸kHz) نیاز به resample.

## پیاده‌سازی فعلی (فاز ۶ — `ArkaCallCenter.Realtime`)

روش انتخابی: **AudioSocket** (ساده‌تر از externalMedia RTP).

- **ورکر:** یک BackgroundService (`AudioSocketServer`) روی پورت TCP `9092` گوش می‌دهد.
- **dialplan:** فایل `telephony/extensions_arka.conf` تماس داخلی را `Answer` و سپس با
  `AudioSocket(<UUID>, worker:9092)` به ورکر وصل می‌کند.
- **نگاشت داخلی:** ۱۲ رقم آخر UUID = شماره‌ی داخلیِ صفرپرشده (اعشاری).
  `AudioSocketProtocol.ParseExtension` آن را استخراج می‌کند
  (مثلاً `...-000000001005` → داخلی ۱۰۰۵).
- **جریان هر تماس (`CallHandler`):**
  1. خواندن UUID → یافتن `SmartPhone` فعال + کاربر + پایگاه دانش.
  2. ساخت instructions شامل کل پایگاه دانش + قانون fallback (گفتن دقیق جمله‌ی
     «پاسخ در پایگاه دانش نیست» در صورت نبود پاسخ).
  3. اتصال به OpenAI Realtime (`OpenAiRealtimeClient`) با گوینده‌ی کاربر.
  4. `GreetAsync` → پخش پیام خوش‌آمد.
  5. صدای caller (SLIN 8kHz) → upsample به ۲۴kHz → `input_audio_buffer.append`.
  6. صدای پاسخ (PCM16 24kHz) → downsample به ۸kHz → فریم‌های AudioSocket.
  7. اعمال سقف دقیقه‌ی مکالمه؛ ثبت `CallSession` (مدت، آیا از KB پاسخ داده شد).
- **صوت:** `AudioResampler` تبدیل خطی ۸k↔۲۴k. فرمت realtime = `pcm16`.

## نکات تنظیم برای محیط واقعی (TODO)
- ماژول `app_audiosocket` باید در استریسک فعال باشد (Asterisk ≥ 16).
- تشخیص نوبت (VAD) اکنون سمت سرور OpenAI است (`server_vad`)؛ barge-in را می‌توان با
  قطع خروجی هنگام صحبت caller بهبود داد.
- برای صرفه‌جویی کامل توکن، به‌جای گفتن جمله‌ی fallback توسط مدل، می‌توان فایل صوتی
  از پیش‌ساخته (`fallback.mp3` پنل سوپرادمین) را مستقیم پلی کرد (نیازمند دیکود mp3→slin).
- بلوک PJSIP در provisioning و context `arka-ai` باید با پیکربندی ایزابل هماهنگ شود.
