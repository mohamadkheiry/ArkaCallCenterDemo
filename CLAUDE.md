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
- **AI:** OpenAI Chat برای بررسی مستقیم کل پایگاه دانش، `gpt-realtime` برای مکالمهٔ تلفنی، TTS برای پیام‌های از پیش‌ساخته (fallback / welcome).
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
2. **پایگاه دانش:** حداکثر **یک** منبع فعال: یا متن ≤ **۲۰۰۰ کاراکتر** یا فایل **txt/docx ≤ ۱۰۰KB**. متن استخراج‌شده برای پاسخ مستقیم حداکثر ۹۰٬۰۰۰ کاراکتر است.
3. **Moderation:** هر ورودی متن یا فایل قبل از فعال‌شدن باید با LLM از نظر انطباق با قوانین ج.ا.ایران بررسی شود. اگر `Rejected` → حذف فایل + پیام به کاربر + رویداد `KnowledgeBaseRejected`.
4. **ساخت تلفن هوشمند:** با کلیک «ایجاد تلفن هوشمند» → تخصیص داخلی آزاد تصادفی در [۱۰۰۰,۹۹۹۹] (unique، تضمین عدم تکرار) → Provisioning روی ایزابل → رویداد `SmartPhoneCreated` (پیامک).
5. **پاسخ‌گویی تماس:** پلی «وویس خوش‌آمد» → انتظار برای سؤال → پاسخ اجتماعی/هویتی یا بررسی مستقیم کل KB با Chat همراه حداکثر شش نوبت اخیر همان تماس → اعتبارسنجی ۱ تا ۴ شناسهٔ `evidenceIds` دقیق و متعلق به snapshot فعلی → خواندن متن عینی قطعه‌های منبع با Realtime. تاریخچه فقط برای فهم سؤال پیرو و ترجیح صریح است و با پایان تماس حذف می‌شود. انتخاب شخصی بدون معیار به سؤال تکمیلی می‌رود؛ پاسخ ناموجود به اپراتور ارجاع و ثبت می‌شود؛ سؤال خارج حوزه فقط محدودهٔ خدمات را معرفی می‌کند.
6. **محدودیت مکالمه:** بر حسب دقیقه. مقدار پیش‌فرض سراسری در `AppSettings`؛ سوپرادمین می‌تواند per-user override کند (`User.CallMinuteLimit`). نزدیک/رسیدن به سقف → رویدادهای مربوطه.
7. **گوینده:** کاربر گوینده‌ی خود را از `VoiceOption`های فعال انتخاب می‌کند؛ پیش‌فرض از تنظیمات سوپرادمین.
8. **CRM فروش:** رویدادهای `PhoneEntered`، `ProfileCompleted` و `SmartPhoneCreated` هرکدام حداکثر یک‌بار پس از موفقیت ارسال می‌شوند. قرارداد عملیاتی: Login در `/api/User/Login`، دریافت Bearer token و ثبت multipart در `/api/ContactUs/InsertContactUsByAdmin`. رمز فقط در secret setting/environment است و هرگز در Git یا لاگ قرار نمی‌گیرد.

---

## ۵. طراحی پاسخ مستقیم دانشی

- `DirectKnowledgeAnswerService` فقط `KnowledgeBase` تأییدشدهٔ همان `UserId` را می‌خواند.
- کل `RawText` به قطعه‌های server-generated از شکل `{i,t}` در `fullKnowledgeBaseSegments` تبدیل می‌شود. نام برند، پیام خوش‌آمد، سؤال، قطعه‌ها و `conversationHistory` با `JsonSerializer` به‌عنوان data غیرقابل‌اعتماد ارسال می‌شوند و system prompt مدل را ملزم می‌کند دستورهای تعبیه‌شده در داده را نادیده بگیرد. تاریخچه حداکثر شش نوبت و ۸۰۰ کاراکتر در هر نوبت، فقط با نقش‌های `user`/`assistant` است؛ منبع واقعیت نیست و فقط ارجاع یا ترجیح صریح تماس‌گیرنده را روشن می‌کند.
- قرارداد JSON مدل فقط `classification` با یکی از `answerable`، `needs_clarification`، `in_domain_unknown` یا `out_of_domain` و آرایهٔ `evidenceIds` است؛ سرویس آن‌ها را به `DirectKnowledgeOutcome` نگاشت می‌کند. `answerable` به ۱ تا ۴ شناسهٔ canonical (`S` + شش رقم ASCII)، یکتا و متعلق به همان درخواست نیاز دارد و سه حالت دیگر باید آرایهٔ خالی بدهند. سرور شناسه‌ها را روی همان snapshot فعلی resolve و فیلد متن آزاد اضافی را نادیده می‌گیرد. guard قطعیِ انتخاب شخصیِ فاقد معیار پیش از Chat، حدس مدل را مسدود می‌کند.
- در مسیر فعلی تماس `RagService.RetrieveAsync`، embedding، chunking، BM25، RRF و Top-K اجرا نمی‌شوند. جدول `KnowledgeChunk` فقط برای rollback حفظ شده است.
- متن هرگز truncate نمی‌شود. سقف‌های ورودی fail-closed عبارت‌اند از: حداکثر ۹۰٬۰۰۰ کاراکتر خام، ۵٬۰۰۰ قطعه، ۱٬۰۰۰ کاراکتر برای هر قطعه، ۱۸۰٬۰۰۰ کاراکتر payload و برآورد ۱۰۰٬۰۰۰ توکن prompt؛ عبور از این موارد `KnowledgeBaseTooLarge` است. timeout برابر ۲۵ ثانیه، سقف completion همین فراخوانی ۳۰۰ توکن و timeout/JSON خراب/خطای provider برابر `ServiceUnavailable` است. انتخاب ID نامعتبر یا مجموع متن منتخب بیش از ۱٬۲۰۰ کاراکتر به `InDomainUnknown` تنزل و به‌عنوان سؤال بی‌پاسخ ثبت می‌شود.
- Base URL، API key و مدل Chat از `AppSettings` (override) یا `.env` خوانده می‌شوند.

---

## ۶. طراحی تلفنی (فاز ۶)

جریان: تماس ورودی به داخلی کاربر → dialplan آن را به Stasis app (`arka-ai`) در ARI می‌فرستد → `ArkaCallCenter.Realtime`:
1. کانال را answer می‌کند و یک `externalMedia`/AudioSocket bridge می‌سازد (فرمت مثلاً slin16/g711).
2. وویس خوش‌آمد را پلی می‌کند.
3. صدای caller را از bridge می‌گیرد و به WebSocket `gpt-realtime` استریم می‌کند.
4. متن transcript ابتدا از social/identity guard و در غیر این صورت از پاسخ مستقیم کل KB همراه تاریخچهٔ محدود همان تماس عبور می‌کند.
5. پاسخ تأییدشده یا متن fallback/معرفی حوزه به Realtime داده می‌شود تا فقط همان متن را طبیعی بخواند.
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
- [x] **فاز ۳ — پایگاه دانش مستقیم + Moderation:** `DirectKnowledgeAnswerService` کل RawText تأییدشدهٔ همان کاربر را با Chat بررسی می‌کند؛ مدل فقط `evidenceIds` را انتخاب می‌کند و سرور متن دقیق segmentهای متناظر را برمی‌گرداند. legacy text-evidence و fuzzy matching پذیرفته نمی‌شوند. FileTextExtractor از txt/docx پشتیبانی می‌کند؛ KnowledgeBaseService moderation، سقف ۹۰٬۰۰۰ و پاک‌کردن chunkهای مشتقِ قدیم را انجام می‌دهد. `RagService` و `KnowledgeChunk` فقط برای rollback باقی مانده‌اند و مسیر تماس آن‌ها را فراخوانی نمی‌کند.
- [x] **فاز ۴ — SMS.ir + پنل سوپرادمین:** تب «OpenAI و پاسخ دانشی» مدل Chat مستقیم، Realtime و TTS را تنظیم می‌کند؛ کنترل‌های RAG/Embedding legacy در UI فعال نیستند. سایر بخش‌ها SMS.ir، رویدادها، گوینده‌ها، fallback و کاربران را پوشش می‌دهند.
- [x] **فاز ۵ — تخصیص داخلی + Provisioning + ساخت تلفن هوشمند:** ExtensionAllocator (تصادفی آزاد ۱۰۰۰–۹۹۹۹، Extension حالا nullable + migration)، AsteriskProvisioningService (SSH.NET، نوشتن بلوک PJSIP + reload؛ در نبود SSH شبیه‌سازی)، SmartPhoneService (پیش‌نیازها، تخصیص، provisioning، SIP secret، تولید وویس خوش‌آمد TTS، پیامک SmartPhoneCreated). کنترلر `smartphone` (GET/POST/PUT welcome). فرانت: SmartPhonePage (پیام خوش‌آمد + چک‌لیست پیش‌نیاز + دکمه ساخت + نمایش داخلی) + آیتم منو. ⚠️ بلوک PJSIP ممکن است بسته به پیکربندی ایزابل نیاز به تنظیم داشته باشد.
- [x] **فاز ۶ — پل تلفنی realtime:** `OpenAiRealtimeClient` transcription و خواندن عینی متن تأییدشده را انجام می‌دهد؛ `CallHandler` پس از social/identity guard، سرویس کل KB را فراخوانی و سپس متن نهایی را literal پخش می‌کند. AudioSocketServer روی TCP:9092، سقف دقیقه، fallback و ثبت CallSession حفظ شده‌اند.

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
