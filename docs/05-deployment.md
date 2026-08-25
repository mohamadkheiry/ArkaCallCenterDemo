# راهنمای استقرار و پیکربندی — کال سنتر هوشمند آرکا

## ۱. پیش‌نیازها
- Docker + Docker Compose روی ماشین در همان شبکه‌ی مرکز تلفن (LAN).
- دسترسی SSH به سرور ایزابل برای نصب dialplan و آپلود صداها.
- کلید OpenAI، توکن SMS.ir و اطلاعات ورود CRM.

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

### اعمال پاسخ مستقیم از کل پایگاه دانش

سرویس `realtime` ابتدا نوبت‌های اجتماعی/کنترلی را مستقیم پاسخ می‌دهد. برای سؤال واقعی، `DirectKnowledgeAnswerService` در هر درخواست کل `RawText` تأییدشدهٔ همان کاربر را به قطعه‌های `{i,t}` در `fullKnowledgeBaseSegments` تبدیل و همراه حداکثر شش نوبت اخیر همان تماس به مدل Chat می‌دهد. تاریخچه برای فهم سؤال پیرو است و شاهد پاسخ نیست. قرارداد مدل `classification` (`answerable` / `needs_clarification` / `in_domain_unknown` / `out_of_domain`) و `evidenceIds` است؛ حالت `answerable` باید ۱ تا ۴ ID canonical/یکتا/شناخته‌شده و سه حالت دیگر آرایهٔ خالی بدهند. در درخواست انتخاب شخصی، نبود معیار صریح کاربر پیش از Chat نیز کنترل می‌شود تا مدل حدس نزند. سرور IDها را روی همان snapshot فعلی به متن منبع نگاشت، whitespace را برای خواندن عادی می‌کند و هر فیلد متن آزاد را نادیده می‌گیرد. در مسیر تماس embedding، chunk retrieval یا Top-K RAG اجرا نمی‌شود. انتخاب ID نامعتبر به سؤال مرتبطِ بی‌پاسخ تنزل و ثبت می‌شود؛ KB خالی نیز ثبت و به اپراتور ارجاع می‌شود؛ سؤال خارج حوزه و timeout/JSON خراب/اختلال provider unanswered نمی‌سازند. متن بی‌صدا کوتاه نمی‌شود و پیش‌بررسی fail-closed شامل حداکثر ۹۰٬۰۰۰ کاراکتر خام، ۵٬۰۰۰ segment، ۱٬۰۰۰ کاراکتر برای هر segment، ۱۸۰٬۰۰۰ کاراکتر payload و برآورد ۱۰۰٬۰۰۰ توکن prompt است. تنظیم قطع تماس پس از سکوت کامل:

```env
CALL_IDLE_TIMEOUT_SECONDS=60
OPENAI_REALTIME_MODEL=gpt-realtime-2.1
OPENAI_TRANSCRIPTION_MODEL=gpt-4o-transcribe
TRANSCRIPTION_LANGUAGE=fa
DEFAULT_VOICE=marin
```

مقدار `0` قطع خودکار را غیرفعال می‌کند. پس از انتشار حداقل شش مسیر را جدا تست کنید: احوال‌پرسی، سؤال دارای پاسخ صریح در KB، سؤال پیرو با ترجیح گفته‌شده، سؤال پیرو بدون معیار (انتظار سؤال تکمیلی)، سؤال مرتبطِ بی‌پاسخ و سؤال کاملاً خارج از حوزه. transcript، `AnsweredFromKb`، `UnansweredQuestionsJson`، log مسیر و `TokenUsage` باید تطبیق داده شوند؛ در نوبت دانشی درست یک Chat و صفر Embedding جدید انتظار می‌رود. حافظه باید با قطع تماس پاک شود و تماس بعدی نباید تاریخچهٔ قبلی را ببیند. شنیدن پیام خوش‌آمد به‌تنهایی قبولی AudioSocket نیست.

## ۳. اسرار (هرگز در گیت نباشند)
در فایل `.env` / `appsettings.Local.json`:
- `OPENAI_API_KEY`
- توکن و قالب‌های SMS.ir
- نام کاربری و رمز CRM عملیاتی (`CRM_USERNAME` / `CRM_PASSWORD` یا تنظیمات ماسک‌شدهٔ پنل)
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
