# CLAUDE.md — راهنمای توسعه‌ی Arka Call Center

> این فایل **منبع حقیقت (source of truth)** برای توسعه‌ی این پروژه است. قبل از هر تغییری آن را بخوانید و بعد از هر تغییر مهم، بخش «وضعیت فازها» را به‌روز کنید.
> زبان محصول: فارسی (RTL). زبان کد/کامیت: انگلیسی.

---

## ۱. هدف محصول

یک سامانه‌ی چند-مستأجری که به هر «کاربر» (صاحب کسب‌وکار) یک **تلفن هوشمند** می‌دهد:
یک **داخلی (extension)** روی سرور ایزابل که تماس‌های ورودی را با هوش مصنوعی OpenAI (`gpt-realtime`) و بر اساس **پایگاه دانش اختصاصی همان کاربر** پاسخ می‌دهد.

سه نقش:
- **User** — صاحب کسب‌وکار؛ لاگین با موبایل، آنبوردینگ، مدیریت پایگاه دانش، انتخاب گوینده.
- **SuperAdmin** — تنظیمات سراسری (SMS.ir، OpenAI، محدودیت‌ها، قالب پیامک‌ها، رویدادها، پیام fallback).
- **Caller** — تماس‌گیرنده‌ی نهایی که با AI صحبت می‌کند (نقش نرم‌افزاری ندارد).

---

## ۲. استک و تصمیمات معماری

- **Backend:** .NET 9 Web API، Clean Architecture سه‌لایه:
  - `ArkaCallCenter.Core` — Entities، Enums، DTOs، Interfaces (بدون وابستگی خارجی).
  - `ArkaCallCenter.Infrastructure` — EF Core/MySQL، پیاده‌سازی سرویس‌های خارجی (OpenAI, SMS.ir, Asterisk)، Repositoryها، Migrations.
  - `ArkaCallCenter.Api` — Controllerها، Auth/JWT، DI، Middleware، Swagger.
  - `ArkaCallCenter.Realtime` — Worker مستقل برای پل صوتی تلفن ⇄ OpenAI Realtime (فاز ۶).
- **DB:** MySQL 8 via EF Core 9 + `Pomelo.EntityFrameworkCore.MySql`. Migrations در Infrastructure.
- **Frontend:** React 18 + Vite + TypeScript + Tailwind + Vazirmatn، کاملاً RTL و ریسپانسیو. State: React Query + Context. روتینگ: react-router.
- **AI:** OpenAI — Embeddings برای RAG، `gpt-realtime` برای مکالمه‌ی تلفنی، TTS برای پیام‌های از پیش‌ساخته (fallback / welcome).
- **SMS:** SMS.ir (REST v1).
- **Telephony:** Isabel = توزیع مبتنی بر Asterisk. ادغام از طریق **ARI + externalMedia/AudioSocket**.

**اصل مهم امنیتی:** هیچ سِری (کلید/رمز/توکن) در گیت نیست. همه در `.env` یا `appsettings.Local.json` (در `.gitignore`). مقادیر قابل‌تنظیم از پنل در جدول `AppSettings` رمزنگاری‌شده ذخیره می‌شوند.

---

## ۳. مدل دامنه (Entities)

| Entity | توضیح |
|--------|-------|
| `User` | صاحب کسب‌وکار. فیلدها: `Id`, `PhoneNumber`(unique), `FirstName`, `LastName`, `BrandName`, `Role`, `CreatedAt`, `IsActive`, `CallMinuteLimit`(nullable override), `VoiceName`. |
| `SmartPhone` | تلفن هوشمند کاربر. `Id`, `UserId`, `Extension`(1000–9999, unique), `WelcomeMessageText`, `WelcomeAudioPath`, `Status`, `CreatedAt`. |
| `KnowledgeBase` | پایگاه دانش. `Id`, `UserId`, `SourceType`(Text/File), `RawText`, `FileName`, `FilePath`, `FileSizeBytes`, `CharCount`, `ModerationStatus`, `CreatedAt`, `UpdatedAt`. یک KB فعال به‌ازای هر کاربر. |
| `KnowledgeChunk` | تکه‌های embedding‌شده. `Id`, `KnowledgeBaseId`, `Content`, `Embedding`(JSON/blob float[]), `ChunkIndex`. |
| `OtpCode` | `Id`, `PhoneNumber`, `Code`, `ExpiresAt`, `Consumed`, `Attempts`. |
| `CallSession` | لاگ تماس. `Id`, `SmartPhoneId`, `CallerId`, `StartedAt`, `EndedAt`, `DurationSeconds`, `AnsweredFromKb`(bool), `TranscriptJson`. |
| `AppSettings` | تنظیمات سراسری key/value (سوپرادمین). |
| `SmsTemplate` | قالب پیامک هر رویداد: `EventType`, `Body`, `Enabled`. |
| `SmsEventRecipient` | شماره‌های گیرنده‌ی هر رویداد: `EventType`, `PhoneNumber`, `UseUserOwnNumber`(bool). |
| `VoiceOption` | گوینده‌های مجاز: `Name`, `DisplayName`, `Provider`, `IsDefault`, `Enabled`. |
| `AuditLog` | لاگ عملیات حساس. |

### Enums
- `UserRole { User, SuperAdmin }`
- `KbSourceType { Text, File }`
- `ModerationStatus { Pending, Approved, Rejected }`
- `SmartPhoneStatus { Provisioning, Active, Suspended, Failed }`
- `SmsEventType { OtpRequested, UserRegistered, SmartPhoneCreated, KnowledgeBaseRejected, KnowledgeBaseUpdated, CallLimitNearlyReached, CallLimitReached, NewCallReceived, SystemAlert }`

---

## ۴. قوانین کسب‌وکار (Business Rules)

1. **لاگین:** فقط با شماره موبایل + OTP (پیامک via SMS.ir). اولین ورود = ثبت‌نام؛ سپس دریافت نام/نام‌خانوادگی/برند.
2. **پایگاه دانش:** حداکثر **یک** منبع فعال: یا متن ≤ **۲۰۰۰ کاراکتر** یا فایل **txt/pdf ≤ ۱۰۰KB**. کاربر می‌تواند بعداً حذف/اضافه/به‌روزرسانی کند.
3. **Moderation:** هر ورودی متن یا فایل قبل از فعال‌شدن باید با LLM از نظر انطباق با قوانین ج.ا.ایران بررسی شود. اگر `Rejected` → حذف فایل + پیام به کاربر + رویداد `KnowledgeBaseRejected`.
4. **ساخت تلفن هوشمند:** با کلیک «ایجاد تلفن هوشمند» → تخصیص داخلی آزاد تصادفی در [۱۰۰۰,۹۹۹۹] (unique، تضمین عدم تکرار) → Provisioning روی ایزابل → رویداد `SmartPhoneCreated` (پیامک).
5. **پاسخ‌گویی تماس:** پلی «وویس خوش‌آمد» → انتظار برای سوال → RAG روی پایگاه دانش → پاسخ realtime. اگر پاسخ در KB نبود → پلی **پیام fallback از پیش‌ساخته** (متن/وویسِ تنظیم‌شده در پنل سوپرادمین) به‌جای تولید realtime (صرفه‌جویی توکن).
6. **محدودیت مکالمه:** بر حسب دقیقه. مقدار پیش‌فرض سراسری در `AppSettings`؛ سوپرادمین می‌تواند per-user override کند (`User.CallMinuteLimit`). نزدیک/رسیدن به سقف → رویدادهای مربوطه.
7. **گوینده:** کاربر گوینده‌ی خود را از `VoiceOption`های فعال انتخاب می‌کند؛ پیش‌فرض از تنظیمات سوپرادمین.

---

## ۵. طراحی RAG

- منبع KB → **chunking** (مثلاً ~۵۰۰ کاراکتر با overlap). چون سقف ۲۰۰۰ کاراکتر/۱۰۰KB است، تعداد chunkها کم است.
- هر chunk → `text-embedding-3-small` → بردار در `KnowledgeChunk.Embedding` (JSON float[]).
- هنگام تماس: سوالِ کاربر (از realtime transcript) → embedding → **cosine similarity** با chunkها (محاسبه در حافظه؛ حجم کوچک است) → top-k.
- اگر بیشترین شباهت < آستانه (`AppSettings.RagSimilarityThreshold`) → «پاسخ در پایگاه دانش نیست» → fallback.
- در غیر این صورت، chunkهای بازیابی‌شده به‌عنوان context به `gpt-realtime` (system/instructions) داده می‌شوند.
- Base URL و API key از `AppSettings` (override) یا `.env`.

---

## ۶. طراحی تلفنی (فاز ۶)

جریان: تماس ورودی به داخلی کاربر → dialplan آن را به Stasis app (`arka-ai`) در ARI می‌فرستد → `ArkaCallCenter.Realtime`:
1. کانال را answer می‌کند و یک `externalMedia`/AudioSocket bridge می‌سازد (فرمت مثلاً slin16/g711).
2. وویس خوش‌آمد را پلی می‌کند.
3. صدای caller را از bridge می‌گیرد و به WebSocket `gpt-realtime` استریم می‌کند.
4. instructions شامل context بازیابی‌شده از RAG است؛ خروجی صوتی realtime به bridge برگردانده و برای caller پلی می‌شود.
5. اگر RAG پاسخی نداشت → پلی فایل fallback از پیش‌ساخته و توقف استریم realtime.
6. زمان مکالمه شمرده می‌شود؛ در سقف، قطع مؤدبانه.

جزئیات dialplan/ARI و نمونه‌ها در `docs/TELEPHONY.md` و پوشه‌ی `telephony/`.

> نکته: نام مدل realtime در نیازمندی «gpt-realtime-2» ذکر شده؛ چون قابل تغییر است، به‌صورت `OPENAI_REALTIME_MODEL` قابل‌تنظیم گذاشته شده.

---

## ۷. سطح API (طرح اولیه)

```
POST /api/auth/request-otp        { phoneNumber }
POST /api/auth/verify-otp         { phoneNumber, code } -> { token, isNewUser }
POST /api/auth/profile            { firstName, lastName, brandName }         [auth]
GET  /api/me                                                                  [auth]

GET  /api/knowledge-base                                                      [auth]
POST /api/knowledge-base/text     { text }                                    [auth]
POST /api/knowledge-base/file     (multipart, txt/pdf ≤100KB)                 [auth]
DELETE /api/knowledge-base                                                    [auth]

POST /api/smartphone              (ایجاد: تخصیص داخلی + provisioning)         [auth]
GET  /api/smartphone                                                          [auth]
PUT  /api/smartphone/welcome      { welcomeMessageText }                      [auth]
PUT  /api/me/voice                { voiceName }                               [auth]

GET  /api/voices                                                              [auth]
GET  /api/calls                   (لاگ تماس‌های کاربر)                        [auth]

# --- SuperAdmin ---
GET/PUT /api/admin/settings                                                   [superadmin]
GET/PUT /api/admin/sms-templates                                             [superadmin]
GET/PUT /api/admin/sms-events                                                [superadmin]
GET/PUT /api/admin/voices                                                     [superadmin]
GET/PUT /api/admin/fallback-message  (متن + تولید وویس با گوینده‌ی منتخب)     [superadmin]
GET/PUT /api/admin/users/{id}/limit                                          [superadmin]
```

---

## ۸. قراردادها (Conventions)

- **کامیت:** انگلیسی، شیوه‌ی conventional (`feat:`, `fix:`, `docs:`, `chore:`). هر گام منطقی = یک کامیت + پوش به `origin/main`.
- **Remote:** `https://github.com/mohamadkheiry/ArkaCallCenterDemo.git`
- نام‌گذاری C#: PascalCase برای public، Async suffix برای متدهای async.
- نام‌گذاری React: کامپوننت‌ها PascalCase، hookها `useX`.
- همه‌ی رشته‌های UI فارسی؛ کدها/کامنت‌های فنی انگلیسی.
- هیچ secret در appsettings.json کامیت‌شده نباشد؛ فقط placeholder.

---

## ۹. وضعیت فازها  ← **بعد از هر گام به‌روز کن**

- [x] **فاز ۰ — پایه:** ساختار ریپو، مستندات، `.gitignore`، `CLAUDE.md`، `.env.example`.
- [x] **فاز ۱ — بک‌اند پایه:** solution سه‌لایه، Core entities + enums، Infrastructure DbContext/MySQL + migration اولیه + Seeder، Api skeleton (JWT + Swagger + CORS)، Auth OTP (`/api/auth/*`, `/api/me`). ⚠️ migration هنوز روی DB زنده اعمال نشده (نیاز به connection string واقعی MySQL).
- [x] **فاز ۲ — فرانت پایه:** Vite+React+TS، Tailwind v4، Vazirmatn (self-hosted)، RTL، AuthContext (JWT/localStorage)، صفحه‌ی لاگین دو‌مرحله‌ای (موبایل→OTP)، آنبوردینگ (نام/برند)، DashboardLayout (سایدبار ریسپانسیو) + صفحه‌ی اصلی + route guardها. build و رندر تأییدشده.
- [x] **فاز ۳ — پایگاه دانش + RAG + Moderation:** OpenAiService (embeddings/chat/TTS via HttpClient، creds از تنظیمات)، FileTextExtractor (txt + pdf/PdfPig)، ModerationService (fail-closed، JSON)، RagService (chunk/embed/cosine + آستانه)، KnowledgeBaseService (اعتبارسنجی حجم/کاراکتر، moderation، حذف فایل مغایر، indexing، رویدادها)، SmsEventDispatcher. کنترلرها: `knowledge-base` (GET/POST text/POST file/DELETE)، `voices`، `me/voice`. فرانت: صفحات پایگاه دانش (متن/فایل drag-drop) و انتخاب گوینده. ⚠️ تست زنده نیاز به MySQL + کلید OpenAI دارد.
- [x] **فاز ۴ — SMS.ir + پنل سوپرادمین:** SmsIrSender (REST v1، fallback به لاگ در نبود کلید)، AdminController (settings با ماسک سِری، sms-templates، sms-events با چند شماره، voices + پیش‌فرض، fallback-message + تولید TTS، users + محدودیت). فرانت: AdminPage شش‌تب (OpenAI/RAG، SMS.ir، پیامک‌ها/رویدادها، گوینده‌ها، پیام fallback، کاربران).
- [x] **فاز ۵ — تخصیص داخلی + Provisioning + ساخت تلفن هوشمند:** ExtensionAllocator (تصادفی آزاد ۱۰۰۰–۹۹۹۹، Extension حالا nullable + migration)، AsteriskProvisioningService (SSH.NET، نوشتن بلوک PJSIP + reload؛ در نبود SSH شبیه‌سازی)، SmartPhoneService (پیش‌نیازها، تخصیص، provisioning، SIP secret، تولید وویس خوش‌آمد TTS، پیامک SmartPhoneCreated). کنترلر `smartphone` (GET/POST/PUT welcome). فرانت: SmartPhonePage (پیام خوش‌آمد + چک‌لیست پیش‌نیاز + دکمه ساخت + نمایش داخلی) + آیتم منو. ⚠️ بلوک PJSIP ممکن است بسته به پیکربندی ایزابل نیاز به تنظیم داشته باشد.
- [x] **فاز ۶ — پل تلفنی realtime:** پروژه‌ی `ArkaCallCenter.Realtime` (worker): AudioSocketServer (TCP:9092)، AudioSocketProtocol (فریم‌بندی + استخراج داخلی از UUID)، AudioResampler (۸k↔۲۴k)، OpenAiRealtimeClient (WebSocket، session.update، greet، append/receive audio)، CallHandler (یافتن SmartPhone، instructions با KB + قانون fallback، سقف دقیقه، ثبت CallSession). dialplan نمونه در `telephony/extensions_arka.conf`. کنترلر `calls` + صفحه‌ی تماس‌ها در فرانت. ⚠️ نیازمند تنظیم زنده: app_audiosocket، کلید OpenAI، هماهنگی context ایزابل.

---

- [x] **افزوده — رهگیری مصرف توکن:** موجودیت `TokenUsage` + migration، `IUsageContext`/`ITokenUsageTracker`، ثبت مصرف در `OpenAiService` (embedding/chat) و worker realtime، میدل‌ورِ انتساب کاربر از JWT. Adminendpointها: `usage/keys` (به تفکیک کلید API + تاریخ) و `usage/users` (به تفکیک کاربر/موبایل). فرانت: تب «مصرف توکن» با تاریخ شمسی.

- [x] **افزوده — IVR پذیرش، موسیقی انتظار، دموها، پیکربندی کامل ایزابل:**
  - **IVR اصلی:** پیام پذیرش قابل‌تنظیم در پنل (متن+گوینده → WAV ۸kHz → آپلود SCP به ایزابل). dialplan `[arka-main]` پیام را پخش و با `Read` داخلی را می‌گیرد، سپس `Goto(arka-ai,${EXT},1)`.
  - **موسیقی انتظار:** آپلود WAV در پنل → تبدیل به SLIN ۸kHz → worker حین «فکر کردن» (رویداد `input_audio_buffer.speech_stopped`) آن را با pacing ۲۰ms پخش می‌کند و با رسیدن صدای AI قطع می‌کند (write lock مشترک).
  - **دموها (۱–۹۹۹):** `DemoService` + `AdminController` (GET/POST/PUT/DELETE `admin/demos`). هر دمو = یک User با `IsDemo` + SmartPhone (داخلی ۱–۹۹۹ via `AllocateDemoAsync`) + KB + گوینده + محدودیت. نامحدود، بدون moderation، همه‌ی منطق تماس بدون تغییر کار می‌کند. تب «دموها» و «پذیرش و انتظار» در فرانت.
  - **ایزابل:** `telephony/extensions_arka.conf` (contextهای `arka-main`+`arka-ai`)، `pjsip_custom.conf`، و `telephony/README.md` (راهنمای کامل: AudioSocket، DID→IVR، SSH، آپلود صوت). `AudioConvert` (WAV/SLIN/resample) در Infrastructure؛ `UploadSoundAsync` (SCP) در provisioning.
  - migration `DemoAndReception`؛ volume آپلود مشترک بین api و realtime در compose.

## 🎯 وضعیت کلی: همه‌ی ۷ فاز + رهگیری توکن + IVR/دمو/انتظار کامل و پوش‌شده‌اند.
گام‌های باقی‌مانده برای بهره‌برداری واقعی (نه توسعه‌ی کد): راه‌اندازی MySQL و اعمال migrationها، ثبت کلید OpenAI و اطلاعات SMS.ir در پنل سوپرادمین، تنظیم SSH/dialplan ایزابل، و تست end-to-end تماس. جزئیات در همین فایل و `docs/TELEPHONY.md`.

---

## ۱۰. چطور توسعه را ادامه دهم (برای جلسه‌ی بعدی)

1. این فایل + `docs/ARCHITECTURE.md` را بخوان.
2. «وضعیت فازها» را ببین؛ اولین فاز تیک‌نخورده را بردار.
3. کد را بساز، لوکال تست کن (`dotnet build` / `npm run build`)، سپس `git add -A && git commit && git push`.
4. «وضعیت فازها» را تیک بزن و در صورت تغییر معماری، بخش‌های مربوط را ویرایش کن.
5. اسرار واقعی را فقط در `.env` بگذار (هرگز کامیت نکن).
