# Arka Call Center — سامانه تلفن هوشمند مبتنی بر هوش مصنوعی

سامانه‌ای چند-مستأجری (multi-tenant) که به هر کاربر یک «تلفن هوشمند» می‌دهد: یک داخلی روی سرور ایزابل (Asterisk) که تماس‌ها را از روی **پایگاه دانش سؤال‌وجواب و صوت‌های ثابت تأییدشده** پاسخ می‌دهد.

> برای توسعه‌ی گام‌به‌گام، **حتماً ابتدا [`CLAUDE.md`](./CLAUDE.md) را بخوانید** — نقشه‌ی کامل معماری، قراردادها و وضعیت فازها آنجاست.
>
> 🚀 برای **استقرار (روی Ubuntu یا هر سیستم با Docker)** به **[`deployment.md`](./deployment.md)** مراجعه کنید — راه سریع تک‌دستوری با `deploy.sh` و راهنمای کامل.

## قابلیت‌ها (خلاصه)

- **داشبورد کاربر و دمو:** لاگین با OTP، مدیریت تعداد نامحدود سؤال‌وجواب به‌صورت صفحه‌بندی‌شده، جلوگیری از سؤال تکراری، پخش/بازتولید صوت هر پاسخ و تنظیم صوت سؤال بی‌پاسخ.
- **پاسخ دانشی قطعی:** پاسخ هر سؤال هنگام ذخیره با مدل `gemini-2.5-pro-preview-tts` و صدای `Kore` به WAV ثابت تبدیل می‌شود. هنگام تماس هیچ پاسخ تازه‌ای توسط مدل ساخته نمی‌شود؛ فقط فایل صوتی سؤال تطبیق‌یافته پخش می‌شود.
- **تشخیص سؤال:** VAD محلی نوبت گفتار را جدا می‌کند، Whisper صوت را به متن فارسی تبدیل می‌کند، `gemini-3.6-flash` فقط رونوشت را بازسازی می‌کند و تطبیق ترکیبی نگارشی/فازی/معنایی نزدیک‌ترین سؤال معتبر را انتخاب می‌کند.
- **Moderation:** بررسی خودکار انطباق محتوای بارگذاری‌شده با قوانین ج.ا.ایران؛ حذف و اطلاع در صورت مغایرت.
- **تلفن هوشمند:** تخصیص خودکار داخلی آزاد (۱۰۰۰–۹۹۹۹)، ساخت آن روی ایزابل، ارسال پیامک اطلاع‌رسانی.
- **پاسخ‌گویی تماس:** پخش پیام خوش‌آمد بدون VAD → دریافت سؤال → Whisper و بازسازی متن → جست‌وجوی سؤال → پخش صوت ثابت پاسخ؛ در نبود تطبیق، پخش صوت ثابت پیام اختصاصی کاربر و ثبت سؤال در گزارش بی‌پاسخ‌ها.
- **ضبط مکالمه:** ترکیب صدای تماس‌گیرنده و پاسخ پخش‌شده روی timeline واقعی ۲۰ میلی‌ثانیه‌ای؛ وقفه‌های طولانی در فایل نهایی کوتاه می‌شوند بدون بریدن بخش‌های کم‌صدای کلمات.
- **پنل سوپرادمین:** ایجاد سوپرادمین با شماره موبایل، ارتقای کاربران موجود به سوپرادمین، ویرایش کاربران، تنظیم SMS.ir، baseURL/API-key اوپن‌ای‌آی، گوینده‌ی پیش‌فرض، محدودیت مکالمه (دقیقه) کلی و per-user، قالب پیامک‌ها، نگاشت رویداد→پیامک→شماره‌ها، متن/وویس پیام fallback.
- **اتصال CRM فروش:** ثبت سه مرحلهٔ لید با Login عملیاتی، Bearer token و `multipart/form-data`؛ آدرس و اطلاعات ورود از تب «CRM فروش» پنل سوپرادمین قابل تنظیم است.

## استک فنی

| لایه | فناوری |
|------|--------|
| Frontend | React 19 + Vite + TypeScript + Tailwind CSS + Vazirmatn (RTL, ریسپانسیو) |
| Backend | .NET 9 Web API (Clean Architecture) |
| ORM/DB | EF Core 9 + Pomelo → **MySQL** |
| AI | Whisper STT + GapGPT (`gemini-3.6-flash` و `gemini-2.5-pro-preview-tts`) |
| SMS | SMS.ir |
| Telephony | Isabel/Asterisk (ARI + AudioSocket/externalMedia) |

## ساختار پوشه‌ها

```
ArkaCallCenterDemo/
├── backend/            # راه‌حل .NET (Api / Core / Infrastructure / Realtime worker)
├── frontend/           # اپلیکیشن React
├── telephony/          # dialplan، اسکریپت‌ها و طراحی پل صوتی Asterisk
├── docs/               # معماری، استقرار، طراحی تلفنی
├── CLAUDE.md           # راهنمای توسعه (منبع حقیقت)
└── README.md
```

## استقرار (Deployment)

کل سامانه با Docker Compose (چندسکویی — Ubuntu/لینوکس و ویندوز) بالا می‌آید:

```bash
unzip ArkaCallCenter-deploy.zip && cd ArkaCallCenterDemo
chmod +x deploy.sh && ./deploy.sh
```

راهنمای کامل: **[`deployment.md`](./deployment.md)** · زیپ آماده: [`release/ArkaCallCenter-deploy.zip`](./release/ArkaCallCenter-deploy.zip)

آخرین گزارش پذیرش فنی مسیر سؤال‌وجواب صوتی و روش اجرای مجدد smoke test در
[`docs/QA_PIPELINE_ACCEPTANCE.md`](./docs/QA_PIPELINE_ACCEPTANCE.md) ثبت شده است.

آخرین مقصد عملیاتی، آدرس‌ها، شمارش داده‌های منتقل‌شده و مسیر backup در
[`docs/LAST_DEPLOYMENT.md`](./docs/LAST_DEPLOYMENT.md) ثبت شده است.

## شروع سریع (توسعه)

پیش‌نیازها: .NET 9 SDK، Node ≥ ۲۰، MySQL ۸.

```bash
# ۱) اسرار را تنظیم کنید (هرگز کامیت نکنید)
cp .env.example .env            # مقادیر واقعی را در .env بگذارید

# ۲) بک‌اند
cd backend
dotnet restore
dotnet ef database update -p src/ArkaCallCenter.Infrastructure -s src/ArkaCallCenter.Api
dotnet run --project src/ArkaCallCenter.Api

# ۳) فرانت‌اند
cd ../frontend
npm install
npm run dev
```

## امنیت

هیچ اسراری (رمز سرور ایزابل، کلید OpenAI، توکن SMS.ir، JWT secret) در گیت کامیت نمی‌شود؛ همه در `.env`/`appsettings.Local.json` که در `.gitignore` هستند نگهداری می‌شوند. برای مقادیر نمونه به [`.env.example`](./.env.example) نگاه کنید.

راهنمای گروه‌های رسمی بله، محل نگهداری Secretها و چرخه رسیدگی به باگ تا انتشار در [`docs/07-operations-communication.md`](./docs/07-operations-communication.md) قرار دارد.

قرارداد فنی و روش توسعهٔ اتصال CRM در [`docs/CRM.md`](./docs/CRM.md) مستند شده است.
