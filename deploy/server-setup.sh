#!/usr/bin/env bash
# Chuẩn bị VPS lần đầu cho RETRACK (chạy TRÊN VPS, quyền root, MỘT LẦN):
#   bash /opt/retrack/server-setup.sh [--with-swap]
# Việc làm: cài Docker nếu thiếu → tạo thư mục → sinh .env.production với mật khẩu/khoá ngẫu nhiên → mở cổng tường lửa (nếu có ufw) → (tuỳ chọn) tạo swap 2 GB.
# KHÔNG xoá gì, KHÔNG đụng tới hệ thống khác trên máy. Chạy lại an toàn: không ghi đè .env.production đã có.
set -euo pipefail

DIR=${RETRACK_DIR:-/opt/retrack}
cd "$DIR"
[ "$(id -u)" -eq 0 ] || { echo "Cần chạy bằng root"; exit 1; }
[ -f .env.production.example ] || { echo "Không thấy $DIR/.env.production.example — hãy chép thư mục deploy/ lên $DIR trước (xem deploy/README.md)"; exit 1; }

WITH_SWAP=0
for a in "$@"; do
  case "$a" in
    --with-swap) WITH_SWAP=1 ;;
    *) echo "Tham số không hợp lệ: $a"; exit 2 ;;
  esac
done

if ! command -v docker >/dev/null 2>&1; then
  echo ">> Cài Docker (get.docker.com)"
  curl -fsSL https://get.docker.com | sh
fi
docker compose version >/dev/null 2>&1 || { echo "Thiếu 'docker compose' (Compose v2) — cài plugin docker-compose-plugin"; exit 1; }

mkdir -p initdb backups db
chmod 700 backups

if [ -f .env.production ]; then
  echo ">> .env.production đã có — giữ nguyên"
else
  command -v openssl >/dev/null 2>&1 || { echo "Cần openssl để sinh mật khẩu ngẫu nhiên"; exit 1; }
  cp .env.production.example .env.production
  # Mỗi dòng "__AUTO__" thay bằng giá trị ngẫu nhiên riêng (chỉ ký tự hex/base64 an toàn cho .env và chuỗi kết nối).
  while grep -q '=__AUTO__' .env.production; do
    value=$(openssl rand -hex 24)
    sed -i "0,/=__AUTO__/s//=${value}/" .env.production
  done
  chmod 600 .env.production
  echo ">> Đã tạo .env.production (quyền 600) với mật khẩu Postgres và JWT_KEY ngẫu nhiên"
fi

if [ "$WITH_SWAP" -eq 1 ]; then
  if swapon --show | grep -q .; then
    echo ">> Máy đã có swap — bỏ qua"
  else
    echo ">> Tạo swap 2 GB (/swapfile) cho VPS ít RAM"
    fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
    grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
  fi
fi

if command -v ufw >/dev/null 2>&1 && ufw status | grep -q 'Status: active'; then
  API_PORT=$(grep -E '^API_PORT=' .env.production | cut -d= -f2-)
  echo ">> ufw đang bật: mở cổng 22, 80, 443 và ${API_PORT:-8080}"
  ufw allow 22/tcp >/dev/null; ufw allow 80/tcp >/dev/null; ufw allow 443/tcp >/dev/null; ufw allow "${API_PORT:-8080}/tcp" >/dev/null
fi

cat <<EOF

Xong. Việc tiếp theo:
  1. Mở $DIR/.env.production và điền FRONTEND_URL, khoá Cloudinary / PayOS / Google nếu dùng (SEED_DEMO=on|off quyết định có nạp dữ liệu demo lúc dựng DB).
  2. Đẩy code lên nhánh main của GitHub (hoặc chạy tay workflow "Backend CD") — CD sẽ build image và chạy deploy-remote.sh.
  3. Kiểm tra:  curl http://127.0.0.1:${API_PORT:-8080}/health/live
EOF
