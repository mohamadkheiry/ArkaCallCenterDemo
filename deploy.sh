#!/usr/bin/env bash
# ============================================================================
# Arka Call Center — اسکریپت استقرار روی Ubuntu / هر لینوکسی با Docker
# اجرا:  chmod +x deploy.sh && ./deploy.sh
# ============================================================================
set -euo pipefail
cd "$(dirname "$0")"

echo "==> بررسی Docker..."
if ! command -v docker >/dev/null 2>&1; then
  echo "Docker نصب نیست. نصب کنید: https://docs.docker.com/engine/install/ubuntu/"
  exit 1
fi
if ! docker compose version >/dev/null 2>&1; then
  echo "افزونه‌ی docker compose نصب نیست (docker-compose-plugin)."
  exit 1
fi

# ساخت .env از روی نمونه در اولین اجرا
if [ ! -f .env ]; then
  cp .env.deploy.example .env
  echo "==> فایل .env از روی .env.deploy.example ساخته شد."
  echo "    لطفاً مقادیر را ویرایش کنید (به‌ویژه MYSQL_ROOT_PASSWORD, JWT_SECRET, SUPERADMIN_PHONE) و دوباره اجرا کنید."
  echo "    برای ادامه با مقادیر پیش‌فرض، همین حالا Enter را بزنید یا Ctrl+C برای توقف."
  read -r _
fi

echo "==> ساخت و بالا آوردن سرویس‌ها..."
docker compose up -d --build

echo ""
echo "==> وضعیت:"
docker compose ps

# خواندن پورت‌ها از .env (اگر تعریف شده)
WEB_PORT="$(grep -E '^WEB_PORT=' .env | cut -d= -f2 || true)"; WEB_PORT="${WEB_PORT:-8081}"
API_PORT="$(grep -E '^API_PORT=' .env | cut -d= -f2 || true)"; API_PORT="${API_PORT:-8080}"

echo ""
echo "============================================================"
echo " آماده شد."
echo "   داشبورد : http://<SERVER_IP>:${WEB_PORT}"
echo "   API      : http://<SERVER_IP>:${API_PORT}"
echo "   Swagger  : http://<SERVER_IP>:${API_PORT}/swagger"
echo "   Scalar   : http://<SERVER_IP>:${API_PORT}/scalar"
echo "   AudioSocket تلفن: پورت TCP 9092"
echo ""
echo " کد ورود در حالت توسعه در لاگ است:  docker compose logs api | grep SMS"
echo "============================================================"
