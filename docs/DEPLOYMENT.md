# استقرار Arka Call Center (Docker Compose)

کل سامانه (MySQL + API + Realtime worker + فرانت nginx) با یک دستور بالا می‌آید.

> **چندسکویی:** همه‌ی ایمیج‌ها Linux-based هستند (dotnet، node، nginx، mysql)، پس این استک
> عیناً روی **Ubuntu / هر لینوکسی** و همچنین ویندوز (با Docker Desktop) اجرا می‌شود. وابستگی به
> ویندوز وجود ندارد.

## پیش‌نیاز روی سرور
- Docker و Docker Compose (v2)
- دسترسی به اینترنت برای pull ایمیج‌ها و restore پکیج‌ها
- پورت‌های آزاد: `8081` (وب)، `8080` (API)، `9092` (AudioSocket تلفن)

## راه سریع روی Ubuntu (اسکریپت)

```bash
# نصب Docker (اگر ندارید)
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER" && newgrp docker   # تا بدون sudo کار کند

# دریافت پروژه — یکی از دو راه:
#   الف) از زیپِ روی گیت:
unzip ArkaCallCenter-deploy.zip && cd ArkaCallCenterDemo
#   ب) یا clone:
# git clone https://github.com/mohamadkheiry/ArkaCallCenterDemo.git && cd ArkaCallCenterDemo

# اجرا با یک دستور (اسکریپت .env می‌سازد و استک را build و up می‌کند)
chmod +x deploy.sh && ./deploy.sh
```

فایل زیپِ آماده‌ی استقرار در ریشه‌ی ریپو، مسیر [`release/ArkaCallCenter-deploy.zip`](../release/ArkaCallCenter-deploy.zip)
قرار دارد (سورس + compose + Dockerfileها + deploy.sh، بدون node_modules/bin).

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
- **API/Scalar (UI مدرن، همان OpenAPI):** `http://SERVER_IP:8080/scalar`
  (باندل Scalar از CDN بارگذاری می‌شود؛ مرورگر باید اینترنت داشته باشد. Swagger کاملاً آفلاین کار می‌کند.)
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
