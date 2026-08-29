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
1. dialplan تماس داخلی کاربر را با `AudioSocket` به worker وصل می‌کند.
2. پیام خوش‌آمد WAV فوراً و بدون VAD پخش می‌شود؛ ورودی این بازه در تصمیم گفتار دخالت ندارد.
3. صدای `slin` هشت‌کیلوهرتز با noise gate و VAD محلی به نوبت‌های مستقل تقسیم می‌شود.
4. هر نوبت به Whisper OpenAI-compatible ارسال و رونوشت آن با GapGPT فقط از نظر خطای تشخیص گفتار بازسازی می‌شود.
5. پاسخ اجتماعی/هویتی مستقیم مدیریت می‌شود؛ سؤال واقعی با سؤال‌وجواب‌های ذخیره‌شده تطبیق می‌یابد.
6. WAV ثابت پاسخ یا WAV ثابت fallback کاربر پخش می‌شود و سؤال بی‌پاسخ همراه تماس ثبت می‌گردد.
7. شمارش زمان، barge-in، ضبط و idle timeout در worker اعمال می‌شوند.

## نکات فرمت صوت
- AudioSocket ورودی/خروجی را PCM16 mono هشت‌کیلوهرتز می‌گیرد. TTS مدل Gemini WAV ۲۴kHz برمی‌گرداند و `AudioConvert` آن را یک‌بار هنگام ذخیره به WAV/SLIN هشت‌کیلوهرتز تبدیل می‌کند.

## پیاده‌سازی فعلی (فاز ۶ — `ArkaCallCenter.Realtime`)

روش انتخابی: **AudioSocket** (ساده‌تر از externalMedia RTP).

- **ورکر:** یک BackgroundService (`AudioSocketServer`) روی پورت TCP `9092` گوش می‌دهد.
- **dialplan:** فایل `telephony/extensions_arka.conf` تماس داخلی را `Answer` و سپس با
  `AudioSocket(<UUID>, worker:9092)` به ورکر وصل می‌کند.
- **نگاشت داخلی:** ۱۲ رقم آخر UUID = شماره‌ی داخلیِ صفرپرشده (اعشاری).
  `AudioSocketProtocol.ParseExtension` آن را استخراج می‌کند
  (مثلاً `...-000000001005` → داخلی ۱۰۰۵).
- **جریان هر تماس (`QaCallHandler`):**
  1. خواندن UUID → یافتن `SmartPhone` فعال + کاربر + پایگاه دانش.
  2. پخش WAV خوش‌آمد بدون VAD و سپس شروع pump بیست‌میلی‌ثانیه‌ای خروجی.
  3. noise gate + pre-roll + حداقل/حداکثر طول گفتار، PCM نوبت را جدا می‌کنند.
  4. `GapAiService.TranscribeAsync` قرارداد Postman را روی `/v1/audio/transcriptions` اجرا می‌کند؛ سپس cleaner متن را بدون پاسخ‌سازی بازسازی می‌کند.
  5. `ConversationTurnClassifier` سکوت/گفتار بی‌معنا و مکالمات اجتماعی را جدا می‌کند؛ سؤال واقعی به `KnowledgeAnswerService.MatchAsync` می‌رود.
  6. در match فقط فایل ذخیره‌شده خوانده می‌شود. در no-match، fallback همان tenant پخش و سؤال در `UnansweredQuestionsJson` ثبت می‌شود. خطای provider به‌عنوان سؤال دانشی بی‌پاسخ ثبت نمی‌شود.
  7. شروع گفتار جدید پردازش/صدای قبلی را لغو و صف خروجی را پاک می‌کند (barge-in).
  8. اعمال سقف دقیقه و timeout؛ ثبت transcript ساختاریافته، match score، ضبط و مصرف دقیقه.
- **صوت:** ورودی و خروجی worker = PCM16 mono 8kHz؛ فایل‌های پاسخ در volume مشترک `uploads` قرار دارند.

## نکات تنظیم برای محیط واقعی (TODO)
- ماژول `app_audiosocket` باید در استریسک فعال باشد (Asterisk ≥ 16).
- VAD محلی با `SPEECH_START_FRAMES`، `SPEECH_END_SILENCE_MS`، `MINIMUM_SPEECH_MS` و `MAXIMUM_UTTERANCE_SECONDS` تنظیم می‌شود.
- برای تأخیر کمتر، فایل‌های خوش‌آمد قدیمی MP3 را با ذخیره مجدد پیام در پنل به WAV مهاجرت دهید.
- بلوک PJSIP در provisioning و context `arka-ai` باید با پیکربندی ایزابل هماهنگ شود.
