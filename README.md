# Arka Call Center — سامانه تلفن هوشمند مبتنی بر هوش مصنوعی

سامانه‌ای چند-مستأجری (multi-tenant) که به هر کاربر یک «تلفن هوشمند» می‌دهد: یک داخلی روی سرور ایزابل (Asterisk) که تماس‌ها را با OpenAI Realtime و **بررسی مستقیم کل پایگاه دانش اختصاصی هر کاربر** پاسخ می‌دهد.

> برای توسعه‌ی گام‌به‌گام، **حتماً ابتدا [`CLAUDE.md`](./CLAUDE.md) را بخوانید** — نقشه‌ی کامل معماری، قراردادها و وضعیت فازها آنجاست.
>
> 🚀 برای **استقرار (روی Ubuntu یا هر سیستم با Docker)** به **[`deployment.md`](./deployment.md)** مراجعه کنید — راه سریع تک‌دستوری با `deploy.sh` و راهنمای کامل.

## قابلیت‌ها (خلاصه)

- **داشبورد کاربر:** لاگین با شماره موبایل (OTP)، دریافت نام/نام‌خانوادگی/برند، پیام خوش‌آمد، پایگاه دانش (متن ≤۲۰۰۰ کاراکتر یا فایل txt/docx ≤۱۰۰KB)، انتخاب گوینده‌ی صدا.
- **پاسخ مستقیم دانشی:** مدل Chat کل `RawText` تأییدشدهٔ همان کاربر را در هر سؤال بررسی می‌کند؛ پاسخ فقط با شاهد عینی پذیرفته و سپس به‌صورت صوتی خوانده می‌شود. مسیر تماس دیگر embedding یا Top-K RAG اجرا نمی‌کند.
- **Moderation:** بررسی خودکار انطباق محتوای بارگذاری‌شده با قوانین ج.ا.ایران؛ حذف و اطلاع در صورت مغایرت.
- **تلفن هوشمند:** تخصیص خودکار داخلی آزاد (۱۰۰۰–۹۹۹۹)، ساخت آن روی ایزابل، ارسال پیامک اطلاع‌رسانی.
- **پاسخ‌گویی تماس:** پلی وویس خوش‌آمد → دریافت سوال → پاسخ realtime از پایگاه دانش → در نبود پاسخ، پلی پیام fallback از پیش‌ساخته (صرفه‌جویی توکن).
- **ضبط مکالمه:** ترکیب صدای تماس‌گیرنده و پاسخ پخش‌شده روی timeline واقعی ۲۰ میلی‌ثانیه‌ای؛ وقفه‌های طولانی در فایل نهایی کوتاه می‌شوند بدون بریدن بخش‌های کم‌صدای کلمات.
- **پنل سوپرادمین:** ایجاد سوپرادمین با شماره موبایل، ارتقای کاربران موجود به سوپرادمین، ویرایش کاربران، تنظیم SMS.ir، baseURL/API-key اوپن‌ای‌آی، گوینده‌ی پیش‌فرض، محدودیت مکالمه (دقیقه) کلی و per-user، قالب پیامک‌ها، نگاشت رویداد→پیامک→شماره‌ها، متن/وویس پیام fallback.

## استک فنی

| لایه | فناوری |
|------|--------|
| Frontend | React 18 + Vite + TypeScript + Tailwind CSS + Vazirmatn (RTL, ریسپانسیو) |
| Backend | .NET 9 Web API (Clean Architecture) |
| ORM/DB | EF Core 9 + Pomelo → **MySQL** |
| AI | OpenAI Chat با کل پایگاه دانش + `gpt-realtime` + TTS |
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
