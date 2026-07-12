# معماری Arka Call Center

این سند مکمل [`../CLAUDE.md`](../CLAUDE.md) است و روی جزئیات معماری و جریان‌های داده تمرکز دارد.

## نمای C4 سطح Container

```
┌──────────────────────────────────────────────────────────────────────┐
│                              Caller (تماس‌گیرنده)                       │
└───────────────┬──────────────────────────────────────────────────────┘
                │ تماس تلفنی (SIP)
                ▼
┌───────────────────────────┐        ARI/AudioSocket        ┌──────────────────────┐
│  Isabel / Asterisk (PBX)  │◄────────────────────────────►│ ArkaCallCenter.Realtime│
│  daخلی 1000–9999          │                               │  (Worker، پل صوتی)     │
└───────────────────────────┘                               └───────────┬──────────┘
                                                                         │ WebSocket
                                                                         ▼
                                                            ┌────────────────────────┐
                                                            │  OpenAI gpt-realtime   │
                                                            └────────────────────────┘

┌──────────────┐   HTTPS/JWT   ┌───────────────────────────┐   EF Core   ┌──────────┐
│  React SPA   │◄─────────────►│   ArkaCallCenter.Api      │◄───────────►│  MySQL   │
│ (User+Admin) │               │  (Controllers, Services)  │             └──────────┘
└──────────────┘               └────────┬──────────────────┘
                                         │
                        ┌────────────────┼─────────────────┐
                        ▼                ▼                 ▼
                  ┌───────────┐   ┌────────────┐    ┌──────────────┐
                  │  OpenAI   │   │   SMS.ir   │    │ Asterisk SSH │
                  │ Embed/TTS │   │  (پیامک)   │    │ /AMI (provision)│
                  └───────────┘   └────────────┘    └──────────────┘
```

## جریان‌های کلیدی

### الف) ورود کاربر (OTP)
1. `POST /auth/request-otp` → تولید کد ۶رقمی، ذخیره در `OtpCode`، ارسال پیامک (رویداد `OtpRequested`).
2. `POST /auth/verify-otp` → اعتبارسنجی، اگر کاربر جدید بود ساخت `User` و بازگرداندن `isNewUser=true`، صدور JWT.
3. کاربر جدید → فرم نام/نام‌خانوادگی/برند → `POST /auth/profile` → رویداد `UserRegistered`.

### ب) پایگاه دانش
1. متن یا فایل آپلود می‌شود (اعتبارسنجی حجم/کاراکتر/نوع).
2. **Moderation** با LLM: انطباق با قوانین ج.ا. → `Approved`/`Rejected`.
3. اگر تأیید شد → chunk + embedding → `KnowledgeChunk`ها. رویداد `KnowledgeBaseUpdated`.
4. اگر رد شد → حذف فایل + پیام کاربر + رویداد `KnowledgeBaseRejected`.

### ج) ساخت تلفن هوشمند
1. `POST /smartphone` → `ExtensionAllocator` یک عدد آزاد در ۱۰۰۰–۹۹۹۹ می‌گیرد (تراکنشی، unique).
2. `AsteriskProvisioningService` داخلی را روی ایزابل می‌سازد (SSH/AMI + reload).
3. `SmartPhone.Status = Active` → رویداد `SmartPhoneCreated` (پیامک شامل شماره داخلی).

### د) پاسخ به تماس (فاز ۶)
شرح کامل در [`TELEPHONY.md`](./TELEPHONY.md).

## نگاشت نیازمندی‌ها → مؤلفه‌ها

| نیازمندی کاربر | مؤلفه |
|----------------|-------|
| لاگین موبایل | `AuthController` + SMS.ir |
| نام/برند/خوش‌آمد | `User` + `SmartPhone.WelcomeMessageText` |
| پایگاه دانش متن/فایل | `KnowledgeBaseController` + `IFileTextExtractor` |
| RAG | `RagService` (embeddings + cosine) |
| Moderation قوانین ج.ا. | `ModerationService` (LLM) |
| به‌روزرسانی/حذف KB | `KnowledgeBaseController` |
| تخصیص داخلی ۱۰۰۰–۹۹۹۹ | `ExtensionAllocator` |
| ساخت تلفن روی ایزابل | `AsteriskProvisioningService` |
| پیامک اطلاع | `SmsService` + `SmsEventDispatcher` |
| gpt-realtime پاسخ‌گویی | `ArkaCallCenter.Realtime` |
| پیام fallback (توکن) | `AppSettings` + TTS از پیش‌ساخته |
| انتخاب گوینده | `VoiceOption` + `User.VoiceName` |
| تنظیمات سوپرادمین | `AdminController` + `AppSettings` |
```
