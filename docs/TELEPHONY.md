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
3. پخش فوری «وویس خوش‌آمد» از کش WAV مشترک؛ در نبود کش معتبر، greeting آنلاین به‌عنوان fallback.
4. صدای caller → استریم به WebSocket `gpt-realtime`؛ transcription فارسی با مدل تنظیم‌شده و سپس بررسی مستقیم کل پایگاه دانش همان کاربر با Chat.
5. صدای خروجی realtime → برگشت به bridge → پلی برای caller.
6. مسیریابی هر نوبت به پاسخ اجتماعی، پاسخ مستند از KB، سؤال مرتبطِ بی‌پاسخ، سؤال خارج از حوزه یا اختلال بازیابی؛ هر مسیر پیام و گزارش مستقل دارد.
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
  2. ساخت instructions پایه و قانون fallback؛ پایگاه دانش کامل وارد session نمی‌شود.
  3. اتصال به OpenAI Realtime (`OpenAiRealtimeClient`) با گوینده‌ی کاربر.
  4. تبدیل WAV خوش‌آمد به SLIN 8kHz و ورود مستقیم به صف خروجی؛ فقط در نبود WAV، `GreetAsync` اجرا می‌شود.
  5. صدای caller (SLIN 8kHz) → noise gate فریم‌های کم‌دامنه → upsample به ۲۴kHz → `input_audio_buffer.append`؛ مدل `gpt-4o-transcribe` فقط با language hint=`fa` متن نوبت را می‌سازد. prompt واژگانی ارسال نمی‌شود تا در سکوت عبارت‌های زمینه‌ای توهم نشوند.
  6. پس از پایان هر نوبت، هویت کسب‌وکار و نیت‌های اجتماعی/کنترلی فارسی پیش از سرویس دانشی بررسی می‌شوند و پاسخ مستقیم می‌گیرند. برای سؤال واقعی، کل `KnowledgeBase.RawText` تأییدشدهٔ همان کاربر به segmentهای request-local با IDهای `Sxxxxxx` تبدیل و همراه حداکثر شش نوبت اخیر همین تماس به‌صورت JSON data غیرقابل‌اعتماد به مدل Chat داده می‌شود. تاریخچه فقط برای فهم ارجاع، ضمیر و ترجیح صریح کاربر است و منبع واقعیت نیست. قرارداد مدل `classification` با یکی از `answerable`، `needs_clarification`، `in_domain_unknown` یا `out_of_domain` و آرایهٔ `evidenceIds` است؛ `answerable` باید ۱ تا ۴ ID canonical/یکتا/شناخته‌شده و سه حالت دیگر آرایهٔ خالی بدهند. سرور شناسه‌ها را روی همان snapshot فعلی به متن منبع نگاشت، whitespace را برای خواندن عادی می‌کند و متن آزاد مدل را نادیده می‌گیرد. اگر انتخاب شخصی به معیار نگفته‌شده نیاز داشته باشد، پاسخ ساختگی مسدود و سؤال تکمیلی پخش می‌شود. انتخاب ID نامعتبر به `InDomainUnknown` تنزل می‌یابد. تماس دیگر `RetrieveAsync`، embedding، BM25، RRF یا Top-K اجرا نمی‌کند. سؤال مرتبطِ بی‌پاسخ ثبت و به اپراتور ارجاع می‌شود؛ سؤال خارج حوزه فقط حوزه را معرفی می‌کند و JSON خراب/خطای provider/timeout در unanswered ثبت نمی‌شود.
  7. صدای پاسخ (PCM16 24kHz) → downsample به ۸kHz → فریم‌های AudioSocket.
  8. اعمال سقف دقیقه و timeout سکوت؛ ثبت `CallSession`، transcript و مصرف توکن.
- **چرخهٔ حافظه:** تاریخچه داخل متغیر محلی همان `CallHandler` نگه‌داری می‌شود، بین تماس‌ها یا کاربران مشترک نیست و با پایان تماس آزاد می‌شود. هر نوبت پس از پاسخ موفق یا کنترل‌شده ثبت و صف به شش نوبت آخر محدود می‌شود.
- **صوت:** `AudioResampler` تبدیل خطی ۸k↔۲۴k. فرمت realtime = `pcm16`.

## نکات تنظیم برای محیط واقعی (TODO)
- ماژول `app_audiosocket` باید در استریسک فعال باشد (Asterisk ≥ 16).
- تشخیص نوبت (VAD) اکنون سمت سرور OpenAI است (`server_vad`) و آستانه پیش‌فرض `0.62` است؛ barge-in را می‌توان با
  قطع خروجی هنگام صحبت caller بهبود داد.
- برای تأخیر کمتر، فایل‌های خوش‌آمد قدیمی MP3 را با ذخیره مجدد پیام در پنل به WAV مهاجرت دهید.
- بلوک PJSIP در provisioning و context `arka-ai` باید با پیکربندی ایزابل هماهنگ شود.
