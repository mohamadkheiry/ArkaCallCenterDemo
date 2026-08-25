# CLAUDE.md — راهنمای توسعه‌ی Arka Call Center

> این فایل **منبع حقیقت (source of truth)** برای توسعه‌ی این پروژه است. قبل از هر تغییری آن را بخوانید و بعد از هر تغییر مهم، بخش «وضعیت فازها» را به‌روز کنید.
> زبان محصول: فارسی (RTL). زبان کد/کامیت: انگلیسی.

---

## ۱. هدف محصول

یک سامانه‌ی چند-مستأجری که به هر «کاربر» (صاحب کسب‌وکار) یک **تلفن هوشمند** می‌دهد:
یک **داخلی (extension)** روی سرور ایزابل که تماس‌های ورودی را بر اساس **پایگاه دانش سؤال‌وجواب اختصاصی همان کاربر** و با پخش صوت‌های ثابت پاسخ می‌دهد.

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
  - `ArkaCallCenter.Realtime` — Worker مستقل AudioSocket برای VAD، STT، تطبیق سؤال و پخش صوت ثابت.
- **DB:** MySQL 8 via EF Core 9 + `Pomelo.EntityFrameworkCore.MySql`. Migrations در Infrastructure.
- **Frontend:** React 18 + Vite + TypeScript + Tailwind + Vazirmatn، کاملاً RTL و ریسپانسیو. State: React Query + Context. روتینگ: react-router.
- **AI:** Whisper سازگار با OpenAI برای STT؛ GapGPT `gemini-3.6-flash` برای بازسازی رونوشت/داوری معنایی و `gemini-2.5-pro-preview-tts` برای ساخت صوت ثابت پاسخ‌ها.
- **SMS:** SMS.ir (REST v1).
- **Telephony:** Isabel = توزیع مبتنی بر Asterisk. ادغام از طریق **ARI + externalMedia/AudioSocket**.

**اصل مهم امنیتی:** هیچ سِری (کلید/رمز/توکن) در گیت نیست. همه در `.env` یا `appsettings.Local.json` (در `.gitignore`). مقادیر حساس پنل با `IsSecret` علامت‌گذاری و در پاسخ API ماسک می‌شوند؛ جدول فعلی رمزنگاری at-rest ندارد، پس دسترسی به DB/backup باید محدود باشد.

---

## ۳. مدل دامنه (Entities)

| Entity | توضیح |
|--------|-------|
| `User` | صاحب کسب‌وکار. فیلدها: `Id`, `PhoneNumber`(unique), `FirstName`, `LastName`, `BrandName`, `Role`, `CreatedAt`, `IsActive`, `CallMinuteLimit`(nullable override), `VoiceName`. |
| `SmartPhone` | تلفن هوشمند کاربر. `Id`, `UserId`, `Extension`(1000–9999, unique), `WelcomeMessageText`, `WelcomeAudioPath`, `Status`, `CreatedAt`. |
| `KnowledgeBase` | پایگاه دانش. `Id`, `UserId`, `SourceType`(Text/File), `RawText`, `FileName`, `FilePath`, `FileSizeBytes`, `CharCount`, `ModerationStatus`, `CreatedAt`, `UpdatedAt`. یک KB فعال به‌ازای هر کاربر. |
| `KnowledgeAnswer` | یک سؤال‌وجواب مستقل با سؤال نرمال‌شده، ترتیب، متن پاسخ، مسیر/hash/status صوت ثابت. تعداد رکوردها محدودیت محصولی ندارد و API صفحه‌بندی می‌شود. |
| `KnowledgeChunk` | دادهٔ legacy نمایه RAG برای rollback؛ مسیر فعلی تماس از آن نمی‌خواند. |
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
2. **پایگاه دانش فعال تماس:** تعداد نامحدود سؤال‌وجواب صفحه‌بندی‌شده؛ سؤال ≤ **۵۰۰** و پاسخ ≤ **۴۰۰۰** کاراکتر. سؤال نرمال‌شده در هر پایگاه یکتا است. متن/فایل قدیمی برای حفظ داده و rollback پاک نمی‌شود ولی مسیر جدید تماس آن را نمی‌خواند.
3. **Moderation:** هر ورودی متن یا فایل قبل از فعال‌شدن باید با LLM از نظر انطباق با قوانین ج.ا.ایران بررسی شود. اگر `Rejected` → حذف فایل + پیام به کاربر + رویداد `KnowledgeBaseRejected`.
4. **ساخت تلفن هوشمند:** با کلیک «ایجاد تلفن هوشمند» → تخصیص داخلی آزاد تصادفی در [۱۰۰۰,۹۹۹۹] (unique، تضمین عدم تکرار) → Provisioning روی ایزابل → رویداد `SmartPhoneCreated` (پیامک).
5. **پاسخ‌گویی تماس:** پخش پیام خوش‌آمد بدون VAD → جداسازی گفتار با VAD محلی → Whisper → بازسازی محافظت‌شدهٔ متن → پاسخ اجتماعی/هویتی یا تطبیق قطعی/فازی/معنایی با سؤال‌های ذخیره‌شده → پخش همان WAV ثابت. در نبود تطبیق، پیام اختصاصی سؤال بی‌پاسخ پخش و متن سؤال در `CallSession` ثبت می‌شود؛ سکوت یا متن بی‌معنا سؤال بی‌پاسخ محسوب نمی‌شود.
6. **محدودیت مکالمه:** بر حسب دقیقه. مقدار پیش‌فرض سراسری در `AppSettings`؛ سوپرادمین می‌تواند per-user override کند (`User.CallMinuteLimit`). نزدیک/رسیدن به سقف → رویدادهای مربوطه.
7. **گوینده:** کاربر گوینده‌ی خود را از `VoiceOption`های فعال انتخاب می‌کند؛ پیش‌فرض از تنظیمات سوپرادمین.
8. **CRM فروش:** رویدادهای `PhoneEntered`، `ProfileCompleted` و `SmartPhoneCreated` هرکدام حداکثر یک‌بار پس از موفقیت ارسال می‌شوند. قرارداد عملیاتی: Login در `/api/User/Login`، دریافت Bearer token و ثبت multipart در `/api/ContactUs/InsertContactUsByAdmin`. رمز فقط در secret setting/environment است و هرگز در Git یا لاگ قرار نمی‌گیرد.

---

## ۵. طراحی پایگاه دانش سؤال‌وجواب

- `KnowledgeAnswerService` فقط رکوردهای متعلق به `KnowledgeBase.UserId` همان کاربر را می‌خواند؛ سؤال نرمال‌شده در هر پایگاه یکتا است و فهرست با `skip/take` صفحه‌بندی می‌شود.
- هنگام افزودن یا تغییر متن پاسخ، moderation اجرا و سپس صوت WAV هشت‌کیلوهرتز ساخته می‌شود. همان مدل و گوینده Gemini ابتدا از مسیر سالم `/audio/speech` با `response_format=wav` فراخوانی می‌شود. مسیر Google-compatible فقط برای سازگاری ثانویه نگه داشته شده، چون adapter فعلی GapGPT ممکن است با وجود محاسبهٔ audio token، `parts` خالی بدهد. فقط اگر هر دو مسیر مدل اصلی شکست بخورند مدل fallback تنظیم‌شده استفاده می‌شود.
- فایل جدید پیش از تغییر DB در مسیر یکتا و به‌صورت atomic نوشته می‌شود. در ویرایش، فایل قبلی تا موفقیت کامل تولید و `SaveChanges` حفظ می‌شود و سپس حذف می‌گردد؛ بنابراین شکست provider نسخهٔ سالم قبلی را از بین نمی‌برد.
- تطبیق ابتدا exact پس از نرمال‌سازی فارسی، سپس امتیاز ترکیبی token/trigram/edit و در حالت نامطمئن داوری معنایی `gemini-3.6-flash` روی shortlist محدود انجام می‌دهد. مدل فقط `matchedId` متعلق به گزینه‌های همان درخواست را می‌تواند برگرداند و هر ID دیگر رد می‌شود.
- سؤال، گزینه‌ها و رونوشت با `JsonSerializer` به‌عنوان دادهٔ غیرقابل‌اعتماد وارد prompt می‌شوند؛ دستورهای احتمالی تماس‌گیرنده یا متن سؤال مجاز به تغییر قرارداد matcher/cleaner نیستند.
- `RawText`، `KnowledgeChunk`، RAG و `DirectKnowledgeAnswerService` برای نگهداری داده‌های قدیمی و rollback باقی مانده‌اند ولی `QaCallHandler` آن‌ها را فراخوانی نمی‌کند.

---

## ۶. طراحی تلفنی (فاز ۶)

جریان: تماس ورودی به داخلی کاربر → dialplan/AudioSocket → `ArkaCallCenter.Realtime`:
1. `AudioSocketServer` داخلی و caller ID را می‌گیرد و `QaCallHandler` تلفن فعال همان tenant را resolve می‌کند.
2. پیام خوش‌آمد ثابت بدون فعال بودن VAD پخش و صدای ورودی این بازه دور ریخته می‌شود.
3. VAD محلی با noise gate، pre-roll و پایان سکوت، نوبت PCM را جدا می‌کند و WAV هشت‌کیلوهرتز را به `/v1/audio/transcriptions` می‌فرستد.
4. رونوشت با cleaner بازسازی می‌شود؛ سکوت/متن بی‌معنا حذف و پاسخ‌های اجتماعی/هویتی پیش از جست‌وجوی دانش مدیریت می‌شوند.
5. در تطبیق موفق، WAV ذخیره‌شده مستقیماً پخش می‌شود؛ در غیر این صورت WAV پیام اختصاصی سؤال بی‌پاسخ پخش و سؤال در گزارش تماس ثبت می‌شود.
6. barge-in خروجی و پردازش نوبت قبلی را لغو می‌کند؛ ضبط، سقف دقیقه، موسیقی انتظار و idle timeout حفظ شده‌اند.

جزئیات dialplan/ARI و نمونه‌ها در `docs/TELEPHONY.md` و پوشه‌ی `telephony/`.

> نام پروژهٔ worker برای سازگاری استقرار همچنان `Realtime` است، اما مسیر فعال تماس از WebSocket Realtime استفاده نمی‌کند.

---

## ۷. سطح API (طرح اولیه)

```
POST /api/auth/request-otp        { phoneNumber }
POST /api/auth/verify-otp         { phoneNumber, code } -> { token, isNewUser }
POST /api/auth/profile            { firstName, lastName, brandName }         [auth]
GET  /api/me                                                                  [auth]

GET  /api/knowledge-base                                                      [auth]
GET  /api/knowledge-base/answers?skip=0&take=100                              [auth]
POST /api/knowledge-base/answers      { question, answer }                    [auth]
PUT/DELETE /api/knowledge-base/answers/{id}                                   [auth]
POST /api/knowledge-base/answers/{id}/regenerate-audio                        [auth]
GET  /api/knowledge-base/answers/{id}/audio                                   [auth]
GET/PUT /api/knowledge-base/fallback                                          [auth]
GET  /api/knowledge-base/fallback/audio                                       [auth]
POST /api/knowledge-base/text     { text }                                    [auth]
POST /api/knowledge-base/file     (multipart, txt/docx ≤100KB)                [auth]
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
- [x] **فاز ۳ — پایگاه دانش سؤال‌وجواب + Moderation:** CRUD صفحه‌بندی‌شده و نامحدود `KnowledgeAnswer`، یکتایی سؤال نرمال‌شده، صوت ثابت پاسخ، صوت اختصاصی fallback و مسیرهای کاربر/دمو پیاده شده‌اند. متن/فایل و سرویس‌های دانشی قبلی فقط برای حفظ داده و rollback باقی مانده‌اند.
- [x] **فاز ۴ — SMS.ir + پنل سوپرادمین:** تب تنظیمات، GapGPT/Whisper، SMS.ir، رویدادها، گوینده‌ها، fallback و کاربران را پوشش می‌دهد. سوپرادمین سؤال‌وجواب‌های هر دمو را با پخش/بازتولید صوت و صفحه‌بندی مدیریت می‌کند.
- [x] **فاز ۵ — تخصیص داخلی + Provisioning + ساخت تلفن هوشمند:** ExtensionAllocator (تصادفی آزاد ۱۰۰۰–۹۹۹۹، Extension حالا nullable + migration)، AsteriskProvisioningService (SSH.NET، نوشتن بلوک PJSIP + reload؛ در نبود SSH شبیه‌سازی)، SmartPhoneService (پیش‌نیازها، تخصیص، provisioning، SIP secret، تولید وویس خوش‌آمد TTS، پیامک SmartPhoneCreated). کنترلر `smartphone` (GET/POST/PUT welcome). فرانت: SmartPhonePage (پیام خوش‌آمد + چک‌لیست پیش‌نیاز + دکمه ساخت + نمایش داخلی) + آیتم منو. ⚠️ بلوک PJSIP ممکن است بسته به پیکربندی ایزابل نیاز به تنظیم داشته باشد.
- [x] **فاز ۶ — پل تلفنی AudioSocket سؤال‌وجواب:** `QaCallHandler` با VAD محلی، Whisper، cleaner و matcher کار می‌کند و WAV پاسخ یا fallback را مستقیم پخش می‌کند. `AudioSocketServer` روی TCP:9092، barge-in، سقف دقیقه، ضبط، fallback و ثبت سؤال‌های بی‌پاسخ را حفظ می‌کند؛ Realtime API در مسیر فعال نیست.

---

- [x] **افزوده — رهگیری مصرف توکن:** موجودیت `TokenUsage` + migration، `IUsageContext`/`ITokenUsageTracker`، ثبت مصرف در `OpenAiService` (embedding/chat) و worker realtime، میدل‌ورِ انتساب کاربر از JWT. Adminendpointها: `usage/keys` (به تفکیک کلید API + تاریخ) و `usage/users` (به تفکیک کاربر/موبایل). فرانت: تب «مصرف توکن» با تاریخ شمسی.

- [x] **افزوده — IVR پذیرش، موسیقی انتظار، دموها، پیکربندی کامل ایزابل:**
  - **IVR اصلی:** پیام پذیرش قابل‌تنظیم در پنل (متن+گوینده → WAV ۸kHz → آپلود SCP به ایزابل). dialplan `[arka-main]` پیام را پخش و با `Read` داخلی را می‌گیرد، سپس `Goto(arka-ai,${EXT},1)`.
  - **موسیقی انتظار:** آپلود WAV در پنل → تبدیل به SLIN ۸kHz → worker حین «فکر کردن» (رویداد `input_audio_buffer.speech_stopped`) آن را با pacing ۲۰ms پخش می‌کند و با رسیدن صدای AI قطع می‌کند (write lock مشترک).
  - **دموها (۱–۹۹۹ به‌جز ۱۰۰–۳۰۰):** `DemoService` + `AdminController` (GET/POST/PUT/DELETE `admin/demos`). هر دمو = یک User با `IsDemo` + SmartPhone با داخلی انتخابی سوپرادمین + KB + گوینده + محدودیت. بازهٔ ۱۰۰–۳۰۰ برای تلفن‌های انسانی محافظت می‌شود و داخلی تکراری پذیرفته نمی‌شود. بدون moderation، همه‌ی منطق تماس بدون تغییر کار می‌کند. تب «دموها» و «پذیرش و انتظار» در فرانت.
  - **ایزابل:** `telephony/extensions_arka.conf` (contextهای `arka-main`+`arka-ai`)، `pjsip_custom.conf`، و `telephony/README.md` (راهنمای کامل: AudioSocket، DID→IVR، SSH، آپلود صوت). `AudioConvert` (WAV/SLIN/resample) در Infrastructure؛ `UploadSoundAsync` (SCP) در provisioning.
  - migration `DemoAndReception`؛ volume آپلود مشترک بین api و realtime در compose.

- [x] **افزوده — UX حرفه‌ای فرانت:** آیکون‌های lucide-react به‌جای ایموجی در کل داشبورد؛ **تور راهنمای کاربر** (`Tour.tsx`، spotlight روی آیتم‌های سایدبار با `data-tour`، اجرای خودکار اولین ورود + دکمه‌ی ؟ در هدر، کلید `arka_tour_done` در localStorage)؛ **ویزارد راه‌اندازی** (`/setup` — SetupWizard.tsx: شروع/ویدیو → پایگاه دانش → پیام خوش‌آمد → گوینده → ساخت تلفن، با استپر)؛ **ویدیوی آموزشی** (آپلود mp4/webm تا ۳۰۰MB توسط سوپرادمین در تب دموها → `POST/DELETE /api/admin/tutorial-video`؛ استریم ناشناس `GET /api/tutorial-video` با range؛ نمایش در داشبورد کاربر و گام اول ویزارد). nginx `client_max_body_size 300m`؛ FormOptions ۳۵۰MB.
- [x] **افزوده — نمونه‌صدای گوینده‌ها:** `VoiceOption.SampleAudioPath` + migration `VoiceSamples`. ادمین در تب گوینده‌ها: متن نمونه‌ی سراسری (`voice.sampleText`) + دکمه‌ی «تولید نمونه» (TTS با همان گوینده → `POST admin/voices/{name}/sample-generate`) + آپلود mp3 دستی (`sample-file`). استریم ناشناس `GET /api/voices/{name}/sample` (audio tag). کامپوننت مشترک `VoiceSampleButton` (پلیر سراسری — پخش جدید قبلی را قطع می‌کند) در VoicePage، گام گوینده‌ی ویزارد، و تب ادمین. ⚠️ خروجی `GET /api/admin/voices` حالا `{ sampleText, voices }` است نه آرایه.
- [x] **رفع — تور در موبایل:** (۱) باگ وسط‌چین: `translate(-50%,-50%)` با انیمیشن `float-in` (fill: both) تداخل داشت → وسط‌چین با flex wrapper. (۲) در موبایل سایدبار حین گام‌های هدف‌دار خودکار باز می‌شود (`onSidebarChange`) و کارت زیر/بالای آیتم هایلایت‌شده می‌نشیند (وقتی فضای کناری نیست). (۳) اندازه‌گیری rect با polling (تا ۱۰×۱۵۰ms) تا پایان ترنزیشن سایدبار + شنونده‌ی resize. تست شده در viewport ۳۷۵px.
- ⚠️ **درس عملیاتی:** `docker compose up -d --build` گاهی کانتینر را جایگزین نمی‌کند — همیشه `docker compose build X && docker compose up -d --force-recreate --no-deps X`. تست ترنزیشن‌های CSS در تب مخفیِ مرورگرِ ابزار اجرا نمی‌شود (`visibilityState: hidden`) — برای تست، transition را inline غیرفعال کن.

- [x] **افزوده — تغییر شماره/لوگو/آواتار:** تغییر شماره‌ی هر کاربر با OTP به شماره‌ی جدید (`IAuthService.RequestPhoneChangeAsync`/`ConfirmPhoneChangeAsync`، endpointهای `me/phone/request-change` و `confirm-change`)؛ آواتار کاربر (`User.AvatarPath`، `POST/DELETE me/avatar`، استریم ناشناس `GET /api/avatars/{id}`)؛ لوگوی سامانه (`SettingKeys.SystemLogoPath`، `POST/DELETE admin/logo`، استریم `GET /api/branding/logo` + `/info`). فرانت: صفحه‌ی `/profile` (آواتار + تغییر شماره دومرحله‌ای)، آواتار کلیک‌پذیر در هدر، `Logo` با fallback به لوگوی آپلودی، تب ادمین «برندینگ». migration `UserAvatarAndLogo`. همه end-to-end تست شد (تغییر شماره، آپلود/استریم آواتار و لوگو).

- [x] **رفع — گیرندگان پیامک رویدادها مستقل:** `SmsEventDispatcher` از `else if` به دو `if` مستقل تغییر کرد تا بتوان همزمان به «خودِ کاربر» **و** «لیست شماره‌های ثابت» ارسال کرد (یا فقط یکی، یا با غیرفعال‌کردن رویداد به هیچ‌کس). فرانت TemplatesTab: فیلد شماره‌ها دیگر disable نمی‌شود؛ متن راهنمای پویا (`recipientHint`) وضعیت مقصد را نشان می‌دهد. تست: KnowledgeBaseRejected با user+شماره ثابت → پیامک به هر دو رفت. ⚠️ PUT `admin/sms-events` کل لیست را جایگزین می‌کند (UI همیشه همه‌ی رویدادها را می‌فرستد).

- [x] **افزوده — ویرایش کاربر توسط سوپرادمین:** `PUT /api/admin/users/{id}` (نام، نام‌خانوادگی، برند، وضعیت فعال، محدودیت). `GET admin/users` حالا دموها را حذف می‌کند (`!IsDemo`). فرانت: تب «کاربران» با ردیف‌های آکاردئونی قابل‌ویرایش. تست: تغییر برند/نام/محدودیت یک کاربر واقعی.

- [x] **افزوده — ضبط و مدیریت مکالمه‌ها:** `CallSession.RecordingPath` + migration `CallRecording`. worker حین تماس صدای caller و AI را در یک بافر SLIN ۸k ضبط و در پایان به WAV ذخیره می‌کند؛ رونوشت ساختاریافته (نوبت‌های user/assistant با `input_audio_transcription`) به‌صورت JSON (camelCase). Adminendpointها: `GET admin/calls` (فیلتر `from`/`to`/`q` روی تاریخ و شماره/برند + صفحه‌بندی)، `GET admin/calls/{id}` (رونوشت)، `GET admin/calls/{id}/recording` (استریم WAV، authorized→فرانت با blob می‌گیرد)، `DELETE admin/calls/{id}` (+حذف فایل). فرانت: تب «مکالمه‌ها» با فیلتر تاریخ/شماره، پخش، حذف، نمایش متن گفتگو. تاریخ‌ها شمسی با helperهای `faDateTime`/`faDate`/`faDuration` (در UsageTab و CallsPage هم استفاده شد). تنظیم `call.recordingEnabled`. تست: درج مکالمه‌ی مصنوعی + WAV → لیست/فیلتر/جزئیات/استریم/حذف همه درست.

## 🎯 وضعیت کلی: همه‌ی ۷ فاز + رهگیری توکن + IVR/دمو/انتظار + UX + پروفایل/برندینگ کامل و پوش‌شده‌اند.
گام‌های باقی‌مانده برای بهره‌برداری واقعی (نه توسعه‌ی کد): راه‌اندازی MySQL و اعمال migrationها، ثبت کلید OpenAI و اطلاعات SMS.ir در پنل سوپرادمین، تنظیم SSH/dialplan ایزابل، و تست end-to-end تماس. جزئیات در همین فایل و `docs/TELEPHONY.md`.

---

## ۱۰. چطور توسعه را ادامه دهم (برای جلسه‌ی بعدی)

1. این فایل + `docs/ARCHITECTURE.md` را بخوان.
2. «وضعیت فازها» را ببین؛ اولین فاز تیک‌نخورده را بردار.
3. کد را بساز، لوکال تست کن (`dotnet build` / `npm run build`)، سپس `git add -A && git commit && git push`.
4. «وضعیت فازها» را تیک بزن و در صورت تغییر معماری، بخش‌های مربوط را ویرایش کن.
5. اسرار واقعی را فقط در `.env` بگذار (هرگز کامیت نکن).
