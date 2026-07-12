# استقرار Arka Call Center (Docker Compose)

کل سامانه (MySQL + API + Realtime worker + فرانت nginx) با یک دستور بالا می‌آید.

## پیش‌نیاز روی سرور
- Docker و Docker Compose (v2)
- دسترسی به اینترنت برای pull ایمیج‌ها و restore پکیج‌ها
- پورت‌های آزاد: `8081` (وب)، `8080` (API)، `9092` (AudioSocket تلفن)

## گام‌ها

```bash
# ۱) دریافت کد
git clone https://github.com/mohamadkheiry/ArkaCallCenterDemo.git
cd ArkaCallCenterDemo

# ۲) تنظیم متغیرها
cp .env.deploy.example .env
nano .env            # رمز MySQL، JWT_SECRET، SUPERADMIN_PHONE، FRONTEND_ORIGIN را تنظیم کنید

# ۳) ساخت و اجرا
docker compose up -d --build

# ۴) مشاهده وضعیت و لاگ‌ها
docker compose ps
docker compose logs -f api          # API به‌صورت خودکار migration می‌زند و seed می‌کند
```

پس از بالا آمدن:
- **وب/داشبورد:** `http://SERVER_IP:8081`
- **API/Swagger (اگر ASPNETCORE_ENVIRONMENT=Development):** `http://SERVER_IP:8080/swagger`
- **سلامت API:** `http://SERVER_IP:8080/health`
- **AudioSocket تلفن:** پورت TCP `9092` (در dialplan استریسک به این پورت وصل شوید)

## پس از استقرار
1. با `SUPERADMIN_PHONE` وارد شوید (کد OTP در حالت توسعه در لاگ API چاپ می‌شود؛ در تولید از SMS.ir ارسال می‌شود).
2. در **پنل سوپرادمین → OpenAI و RAG**: `Base URL` و `API Key` اوپن‌ای‌آی را وارد کنید.
3. در **پنل سوپرادمین → SMS.ir**: کلید و شماره خط را وارد کنید.
4. در **پیام پیش‌فرض**: متن fallback را ذخیره و صوت آن را تولید کنید.

## اتصال به ایزابل (تلفن)
- سرویس `realtime` روی پورت `9092` گوش می‌دهد.
- در استریسک، فایل `telephony/extensions_arka.conf` را include کنید و
  `ARKA_WORKER_HOST` را برابر IP این سرور قرار دهید.
- ماژول `app_audiosocket` باید فعال باشد. جزئیات در [`TELEPHONY.md`](./TELEPHONY.md).
- Provisioning داخلی از طریق SSH: مقادیر `Asterisk__Host/SshUser/SshPassword` را
  به‌عنوان env به سرویس `api` بدهید (یا در compose اضافه کنید). بدون این‌ها، ساخت
  داخلی شبیه‌سازی می‌شود.

## دستورات مفید
```bash
docker compose down            # توقف
docker compose down -v         # توقف + حذف دیتابیس (احتیاط)
docker compose logs -f realtime
docker compose restart api
```
