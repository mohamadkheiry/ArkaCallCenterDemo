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

## کارهای آینده
- انتخاب دقیق روش (AudioSocket ساده‌تر از externalMedia RTP).
- مدیریت barge-in (قطع پاسخ AI وقتی caller حرف می‌زند).
- VAD و مدیریت نوبت مکالمه.
