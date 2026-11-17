#!/usr/bin/env bash
# CD — chạy TRÊN VPS (GitHub Actions gọi qua SSH, hoặc chạy tay): đưa image API đã build sẵn lên chạy cùng Postgres.
#   IMAGE=ghcr.io/<owner>/retrack-api:<tag> bash /opt/retrack/deploy-remote.sh
# Biến: GHCR_USER + GHCR_TOKEN (để kéo image riêng tư; CD truyền token tạm), HEALTH_TIMEOUT=120 (giây), KEEP_BACKUPS=7, SKIP_PULL=1 (dùng image có sẵn).
# Các bước: sinh file khởi tạo DB → đảm bảo Postgres chạy → sao lưu DB (nếu DB đã có dữ liệu) → ghi image mới → chạy API → chờ khoẻ → dọn.
# Lỗi sau khi đổi image ⇒ tự quay về image cũ (CHỈ code). Schema KHÔNG tự đổi: file SQL chỉ chạy khi volume DB còn trống (lần đầu);
# sửa schema sau đó do nhóm chạy tay (đúng quy ước AGENTS.md: không migrate tự động lên DB dùng chung).
set -euo pipefail
cd "$(dirname "$0")"

: "${IMAGE:?thiếu biến IMAGE (vd ghcr.io/giahensum/retrack-api:<sha>)}"
[ -f .env.production ] || { echo "Thiếu $(pwd)/.env.production — chạy server-setup.sh trước (xem deploy/README.md)"; exit 1; }
command -v docker >/dev/null || { echo "Chưa cài Docker"; exit 1; }

COMPOSE=(docker compose --env-file .env.production -f docker-compose.prod.yml)
HEALTH_TIMEOUT=${HEALTH_TIMEOUT:-120}
KEEP_BACKUPS=${KEEP_BACKUPS:-7}
TS=$(date +%Y%m%d-%H%M%S)
mkdir -p backups initdb db

envval() { grep -E "^$1=" .env.production | tail -1 | cut -d= -f2- || true; }
set_env() {   # set_env KEY VALUE — ghi/cập nhật một dòng trong .env.production (giữ quyền 600)
  if grep -q "^$1=" .env.production; then sed -i "s|^$1=.*|$1=$2|" .env.production
  else
    [ -n "$(tail -c1 .env.production)" ] && echo >> .env.production
    printf '%s=%s\n' "$1" "$2" >> .env.production
  fi
  chmod 600 .env.production
}

POSTGRES_USER=$(envval POSTGRES_USER); POSTGRES_USER=${POSTGRES_USER:-retrack}
POSTGRES_DB=$(envval POSTGRES_DB); POSTGRES_DB=${POSTGRES_DB:-retrack}
SEED_DEMO=$(envval SEED_DEMO); SEED_DEMO=${SEED_DEMO:-off}
API_PORT=$(envval API_PORT); API_PORT=${API_PORT:-8080}
DOMAIN=$(envval DOMAIN)
case "$SEED_DEMO" in on|off) ;; *) echo "SEED_DEMO phải là on hoặc off"; exit 1 ;; esac

# 1) File khởi tạo DB (chỉ có tác dụng khi volume pg_data còn trống)
if [ -f db/retrack-system.sql ]; then
  sed "s/SET LOCAL retrack.seed_demo = '[a-z]*';/SET LOCAL retrack.seed_demo = '${SEED_DEMO}';/" db/retrack-system.sql > initdb/01-retrack-system.sql
  grep -q "retrack.seed_demo = '${SEED_DEMO}'" initdb/01-retrack-system.sql || { echo "Không áp được SEED_DEMO vào file SQL (dòng seed_demo đổi định dạng?)"; exit 1; }
else
  echo "Cảnh báo: không có db/retrack-system.sql — nếu DB còn trống thì API sẽ chạy trên schema rỗng"
fi

# 2) Đăng nhập GHCR (nếu có) và kéo image
PREV=$(envval API_IMAGE)
if [ -n "${GHCR_TOKEN:-}" ]; then echo "$GHCR_TOKEN" | docker login ghcr.io -u "${GHCR_USER:-github}" --password-stdin >/dev/null; fi
[ "${SKIP_PULL:-0}" = 1 ] || docker pull "$IMAGE"

# 3) Compose cần API_IMAGE hợp lệ cho mọi lệnh ⇒ ghi image mới ngay; lỗi thì đặt lại $PREV ở bước 6
set_env API_IMAGE "$IMAGE"

# 4) Postgres lên trước, đợi sẵn sàng
"${COMPOSE[@]}" up -d db
for i in $(seq 1 60); do
  "${COMPOSE[@]}" exec -T db pg_isready -h 127.0.0.1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" >/dev/null 2>&1 && break
  sleep 2
  [ "$i" -lt 60 ] || { echo "Postgres không sẵn sàng sau 120 giây"; "${COMPOSE[@]}" logs --tail 50 db; exit 1; }
done

# 5) Sao lưu DB trước khi đổi code (bỏ qua nếu DB mới tinh: chưa có bảng nào)
TABLES=$("${COMPOSE[@]}" exec -T db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "select count(*) from information_schema.tables where table_schema='public'" 2>/dev/null || echo 0)
if [ "${TABLES:-0}" -gt 0 ]; then
  "${COMPOSE[@]}" exec -T db pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB" > "backups/pre-$TS.dump"
  [ -s "backups/pre-$TS.dump" ] || { echo "Sao lưu DB rỗng — dừng, chưa đổi code"; rm -f "backups/pre-$TS.dump"; set_env API_IMAGE "$PREV"; exit 1; }
  echo "Đã sao lưu DB: backups/pre-$TS.dump"
  ls -1t backups/pre-*.dump 2>/dev/null | tail -n +$((KEEP_BACKUPS + 1)) | xargs -r rm -f
else
  echo "DB mới (chưa có bảng) — bỏ qua sao lưu"
fi

# 6) Chạy API (kèm Caddy khi có DOMAIN), chờ khoẻ, lỗi thì quay về bản cũ
UP=("${COMPOSE[@]}")
[ -n "$DOMAIN" ] && UP+=(--profile tls)
"${UP[@]}" up -d --no-deps api
[ -n "$DOMAIN" ] && "${UP[@]}" up -d caddy

healthy() {
  local waited=0
  while [ "$waited" -lt "$HEALTH_TIMEOUT" ]; do
    curl -fsS "http://127.0.0.1:${API_PORT}/health/live" >/dev/null 2>&1 && return 0
    sleep 3; waited=$((waited + 3))
  done
  return 1
}

if healthy; then
  echo "Triển khai thành công: $IMAGE"
  docker image prune -f >/dev/null || true
  [ -n "${GHCR_TOKEN:-}" ] && docker logout ghcr.io >/dev/null 2>&1 || true
  exit 0
fi

echo "API không khoẻ sau ${HEALTH_TIMEOUT}s — log gần nhất:"
"${COMPOSE[@]}" logs --tail 60 api || true
if [ -n "$PREV" ]; then
  echo "Quay về image trước: $PREV"
  set_env API_IMAGE "$PREV"
  "${UP[@]}" up -d --no-deps api || true
fi
[ -n "${GHCR_TOKEN:-}" ] && docker logout ghcr.io >/dev/null 2>&1 || true
exit 1
