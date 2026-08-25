# اتصال CRM فروش آرکا

این سند قرارداد غیرمحرمانهٔ اتصال `ArkaCallCenterDemo` به CRM عملیاتی را ثبت می‌کند. نام کاربری، رمز، Bearer token و پاسخ‌های حاوی اطلاعات حساس نباید در Git یا لاگ قرار گیرند.

## جریان درخواست

1. `CrmLeadService` با `POST {baseUrl}/api/User/Login` و بدنهٔ JSON شامل `username` و `password` وارد می‌شود.
2. توکن از `result.token` خوانده و فقط در حافظهٔ همان ارسال نگه‌داری می‌شود.
3. لید با `POST {baseUrl}/api/ContactUs/InsertContactUsByAdmin`، هدر `Authorization: Bearer …` و بدنهٔ `multipart/form-data` ارسال می‌شود.
4. موفقیت هم از HTTP status و هم از `success=true` در JSON پاسخ کنترل می‌شود.

Base URL عملیاتی پیش‌فرض:

```text
https://api.arkadp.com
```

## فیلدهای ارسالی

| فیلد فرم | مقدار در سامانه |
| --- | --- |
| `inputModel.Name` | نام و نام خانوادگی و در صورت وجود برند؛ پیش از تکمیل پروفایل نام موقت لید |
| `inputModel.Email` | ایمیل جایگزین ساخته‌شده از شماره و `crm.emailDomain` |
| `inputModel.PhoneNumber` | موبایل نرمال‌شده |
| `inputModel.FeedbackText` | مرحلهٔ لید و اطلاعات مرتبط همان مرحله |
| `inputModel.RequestType` | `2`، درخواست اجرای پروژه |
| `inputModel.RequestSource` | `2`، کال‌سنتر |
| `inputModel.RequestedProject` | `1`، SmartCallCenter |
| `inputModel.FormStatus` | `1`، جدید |

فایل ارسال نمی‌شود. `Content-Type` دستی تنظیم نمی‌شود تا `HttpClient` مقدار boundary را برای multipart تولید کند.

## مراحل لید و جلوگیری از تکرار

- `PhoneEntered`: به‌محض ورود شماره در مسیر OTP پیامکی، حتی پیش از تأیید کد.
- `ProfileCompleted`: پس از ذخیره نام، نام خانوادگی و برند.
- `SmartPhoneCreated`: پس از ساخت موفق داخلی؛ شامل شماره داخلی و خلاصهٔ وضعیت تلفن/دانش.

نتیجه در `CrmLeadSubmissions` ذخیره می‌شود. ترکیب شماره و مرحله یکتا است؛ مرحله‌ای که یک‌بار `Success=true` شده دوباره ارسال نمی‌شود، اما ارسال ناموفق در رویداد بعدی قابل تلاش مجدد است.

## تنظیمات

| کلید پنل / AppSetting | env متناظر | حساس |
| --- | --- | --- |
| `crm.enabled` | `CRM_ENABLED` | خیر |
| `crm.baseUrl` | `CRM_BASE_URL` | خیر |
| `crm.username` | `CRM_USERNAME` | خیر |
| `crm.password` | `CRM_PASSWORD` | بله؛ در API ماسک می‌شود |
| `crm.emailDomain` | `CRM_EMAIL_DOMAIN` | خیر |

کلید قدیمی `crm.apiKey` فقط برای rollback در دیتابیس باقی می‌ماند و قرارداد جدید آن را مصرف نمی‌کند.

## تست پس از تغییر

```bash
dotnet test backend/tests/ArkaCallCenter.Tests/ArkaCallCenter.Tests.csproj -c Release
```

در محیط عملیاتی ابتدا Login را بدون چاپ token تست کنید. برای تست ثبت، لید را با نام و توضیح صریح «تست فنی — قابل حذف» بسازید تا تیم CRM بتواند آن را تشخیص و حذف کند. پس از انتشار، `CrmLeadSubmissions.ResponseMessage` و لاگ ماسک‌شدهٔ API را کنترل کنید؛ هیچ token یا password نباید ثبت شده باشد.
