# Arka Call Center — سامانه تلفن هوشمند مبتنی بر هوش مصنوعی

سامانه‌ای چند-مستأجری (multi-tenant) که به هر کاربر یک «تلفن هوشمند» می‌دهد: یک داخلی روی سرور ایزابل (Asterisk) که تماس‌ها را با هوش مصنوعی (OpenAI `gpt-realtime`) و بر پایه‌ی **پایگاه دانش اختصاصی هر کاربر (RAG)** پاسخ می‌دهد.

> برای توسعه‌ی گام‌به‌گام، **حتماً ابتدا [`CLAUDE.md`](./CLAUDE.md) را بخوانید** — نقشه‌ی کامل معماری، قراردادها و وضعیت فازها آنجاست.

## قابلیت‌ها (خلاصه)

- **داشبورد کاربر:** لاگین با شماره موبایل (OTP)، دریافت نام/نام‌خانوادگی/برند، پیام خوش‌آمد، پایگاه دانش (متن ≤۲۰۰۰ کاراکتر یا فایل txt/pdf ≤۱۰۰KB)، انتخاب گوینده‌ی صدا.
- **RAG:** ساخت embedding از پایگاه دانش با OpenAI و بازیابی مبتنی بر شباهت هنگام پاسخ به تماس.
- **Moderation:** بررسی خودکار انطباق محتوای بارگذاری‌شده با قوانین ج.ا.ایران؛ حذف و اطلاع در صورت مغایرت.
- **تلفن هوشمند:** تخصیص خودکار داخلی آزاد (۱۰۰۰–۹۹۹۹)، ساخت آن روی ایزابل، ارسال پیامک اطلاع‌رسانی.
- **پاسخ‌گویی تماس:** پلی وویس خوش‌آمد → دریافت سوال → پاسخ realtime از پایگاه دانش → در نبود پاسخ، پلی پیام fallback از پیش‌ساخته (صرفه‌جویی توکن).
- **پنل سوپرادمین:** تنظیم SMS.ir، baseURL/API-key اوپن‌ای‌آی، گوینده‌ی پیش‌فرض، محدودیت مکالمه (دقیقه) کلی و per-user، قالب پیامک‌ها، نگاشت رویداد→پیامک→شماره‌ها، متن/وویس پیام fallback.

## استک فنی

| لایه | فناوری |
|------|--------|
| Frontend | React 18 + Vite + TypeScript + Tailwind CSS + Vazirmatn (RTL, ریسپانسیو) |
| Backend | .NET 9 Web API (Clean Architecture) |
| ORM/DB | EF Core 9 + Pomelo → **MySQL** |
| AI | OpenAI Embeddings + `gpt-realtime` + TTS |
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
