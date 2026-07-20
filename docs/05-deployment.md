# راهنمای استقرار و پیکربندی — کال سنتر هوشمند آرکا

## ۱. پیش‌نیازها
- Docker + Docker Compose روی ماشین در همان شبکه‌ی مرکز تلفن (LAN).
- دسترسی SSH به سرور ایزابل برای نصب dialplan و آپلود صداها.
- کلید OpenAI و توکن SMS.ir.

## ۲. استقرار استک
```bash
# ساخت و بالا آوردن (mysql + api + realtime + nginx)
docker compose build
docker compose up -d

# اعمال کدِ جدید یک سرویس خاص:
docker compose build <service>
docker compose up -d --force-recreate --no-deps <service>
```
آدرس‌ها: وب `:8081`، API `:8080`، AudioSocket `:9092`. مهاجرت‌های EF هنگام استارتِ API خودکار اعمال می‌شوند.

### اعمال اصلاحات ضبط مکالمه

منطق ضبط داخل سرویس `realtime` است و برای انتشار این اصلاح، بازسازی دیتابیس یا API لازم نیست:

```bash
docker compose build realtime
docker compose up -d --force-recreate --no-deps realtime
docker compose logs --tail=100 realtime
```

پس از انتشار، یک تماس آزمایشی برقرار کنید و فایل ضبط را از پنل پخش کنید. گفتار دو طرف باید پیوسته باشد و وقفه‌های پردازش AI در فایل ذخیره‌شده حداکثر حدود ۲۸۰ میلی‌ثانیه باقی بماند.

### اعمال RAG نوبت‌به‌نوبت و قطع تماسِ بی‌فعالیت

سرویس `realtime` پیش از ساخت هر پاسخ، متن همان نوبت را در RAG جست‌وجو می‌کند. اگر قطعهٔ مرتبط پیدا نشود، فقط پیام fallback خوانده می‌شود. تنظیم قطع تماس پس از سکوت کامل:

```env
CALL_IDLE_TIMEOUT_SECONDS=60
OPENAI_REALTIME_MODEL=gpt-realtime-2.1
OPENAI_TRANSCRIPTION_MODEL=gpt-4o-transcribe
TRANSCRIPTION_LANGUAGE=fa
DEFAULT_VOICE=marin
```

مقدار `0` قطع خودکار را غیرفعال می‌کند. پس از انتشار، یک سؤال موجود در KB و یک سؤال کاملاً نامرتبط را تست کنید؛ اولی باید پاسخ KB و دومی باید fallback بگیرد.

## ۳. اسرار (هرگز در گیت نباشند)
در فایل `.env` / `appsettings.Local.json`:
- `OPENAI_API_KEY`
- توکن و قالب‌های SMS.ir
- رمزِ root ایزابل (فقط برای provisioning)

## ۴. پیکربندی ایزابل / Asterisk
- نصب dialplan در `/etc/asterisk/extensions_custom.conf` (context های `arka-main` و `arka-ai`).
- تنظیم `ARKA_WORKER_HOST` به IP ماشینِ استقرار و `ARKA_WORKER_PORT=9092`.
- پس از هر ویرایش: `asterisk -rx 'dialplan reload'`.
- در context `arka-ai` از الگوی `_X!` استفاده کنید تا داخلی تک‌رقمی مثل `2` نیز match شود؛ `_X.` فقط داخلی‌های دو رقمی و بیشتر را می‌پذیرد.
- مسیر صداها: `/var/lib/asterisk/sounds/arka/`.

## ۵. اتوماسیون اطلاع‌رسانی صوتی جیرا
- مسیر: `/opt/arka-jira/` روی ایزابل؛ کرانِ هر دقیقه + یادآورِ روزانه.
- TTS فارسی با **piper** (صدای Ganji) در محیطِ conda؛ سرویسِ صوتِ HTTP روی پورت ۸۰۹۹.

## ۶. نکات عملیاتی
- **SSH به ایزابل با paramiko** انجام شود (نه plink؛ plink روی prompt کلیدِ میزبان هنگ می‌کند).
- بازیابیِ پس از قطعِ برق: پایدارسازیِ `eth1` و سرویس‌ها با systemd.
- **مهم (سازگاری با OpenAI Realtime GA):** پارامترِ `temperature` در `session.update` ارسال نشود؛ نسخه‌ی GA آن را حذف کرده و ارسالش کلِ session را رد می‌کند. کنترلِ رفتار از طریقِ پرامپت انجام می‌شود.
- برای مکالمه فارسی، language hint باید `fa` بماند. تغییر مدل transcription ابتدا روی صدای واقعی تلفن تست شود؛ `gpt-4o-transcribe` برای دقت بالاتر از `whisper-1` انتخاب شده است.
- پیام‌های خوش‌آمد جدید WAV هستند، هنگام startup به SLIN 8kHz تبدیل و در حافظه گرم می‌شوند و مستقیم پخش می‌شوند. پاسخ‌های WAV streaming با طول نامشخص نیز پشتیبانی می‌شوند. پس از مهاجرت از نسخه MP3، متن خوش‌آمد را یک بار ذخیره کنید تا فایل WAV تولید شود.
