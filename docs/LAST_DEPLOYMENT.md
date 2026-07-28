# آخرین استقرار ArkaCallCenterDemo

این فایل فقط اطلاعات عملیاتی غیرمحرمانه را ثبت می‌کند. هیچ رمز، API Key یا token
نباید در Git قرار گیرد.

## محیط فعلی

| مورد | مقدار |
| --- | --- |
| تاریخ استقرار | ۲۰۲۶-۰۷-۲۸ |
| سرور | Issabel / Asterisk در `192.168.10.101` |
| commit برنامه | `236f30de59208fcd53acc828e7bc44437f934d3a` |
| مسیر برنامه | `/opt/arka-call-center` |
| پروژه Compose | `arkacallcenterdemo` |
| اجرای خودکار | واحد `arka-call-center.service` و Docker هر دو `enabled`؛ policy کانتینرها `unless-stopped` |

## آدرس‌ها

- داشبورد: <http://192.168.10.101:8081/>
- سلامت برنامه از مسیر وب: <http://192.168.10.101:8081/health>
- سلامت مستقیم API: <http://192.168.10.101:8080/health>
- Swagger: <http://192.168.10.101:8080/swagger/index.html>
- Scalar: <http://192.168.10.101:8080/scalar/v1>
- AudioSocket داخلی Asterisk: `127.0.0.1:9092`

سرویس مستقل OTP یعنی `CodeSenderWithPhone` همچنان روی
<http://192.168.10.101:8100/> فعال است و کال‌سنتر برای ارسال OTP به همان سرویس
متصل می‌شود.

## داده منتقل‌شده از محیط محلی

| داده | تعداد |
| --- | ---: |
| کاربران | ۸ |
| تلفن‌های هوشمند | ۷ |
| پایگاه‌های دانش | ۷ |
| chunkهای RAG | ۷۵ |
| تماس‌ها | ۲۷ |
| کدهای OTP | ۹۹ |
| تنظیمات | ۳۱ |
| گوینده‌ها | ۱۰ |
| رکوردهای مصرف توکن | ۱۱۵ |
| فایل‌های uploads | ۵۳ |

دیتابیس در volume مستقل `arkacallcenterdemo_db_data` و فایل‌ها در
`arkacallcenterdemo_uploads` نگهداری می‌شوند. MySQL اصلی Issabel روی میزبان تغییر
نکرده و MySQL این برنامه فقط داخل شبکه خصوصی Docker است.

## تلفن و بازگشت

contextهای `arka-main`، `arka-ai` و داخلی تست پذیرش `9000` در Asterisk فعال‌اند.
مقصد AudioSocket قدیمی `192.168.10.175:9092` به worker محلی ایزابل یعنی
`127.0.0.1:9092` تغییر کرد. مسیر ورودی DID عمومی در این استقرار تغییر داده نشد تا
تماس‌های جاری شرکت بدون تأیید صریح جابه‌جا نشوند.

بکاپ root-only پیش از/حین انتقال در مسیر زیر است:

```text
/var/backups/arka-call-center/20260728-initial-236f30d
```

این بکاپ شامل dump دیتابیس محلی، uploads، environment و نسخه قبل/بعد dialplan است.

## راه‌اندازی خودکار پس از reboot

واحد systemd با نام `arka-call-center.service` روی سرور نصب، فعال و با restart کنترل‌شده
تست شده است. این واحد بعد از آماده‌شدن شبکه و Docker، استک Compose را از مسیر
`/opt/arka-call-center` بالا می‌آورد. نمونه قابل نصب آن در
[`systemd/arka-call-center.service`](../systemd/arka-call-center.service) نگهداری می‌شود.

وضعیت مورد انتظار:

```bash
systemctl is-enabled arka-call-center.service  # enabled
systemctl is-active arka-call-center.service   # active
```
