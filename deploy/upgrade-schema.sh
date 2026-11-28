#!/usr/bin/env bash
# Nâng schema DB production lên đúng bản db/retrack-system.sql đã chép lên VPS — chạy TAY trên VPS, sau khi nhóm review SQL.
#   bash /opt/retrack/upgrade-schema.sh            # sao lưu rồi áp khối schema
#   CHECK_ONLY=1 bash /opt/retrack/upgrade-schema.sh   # chỉ kiểm tra các cột mới nhất đã có chưa
# Chỉ áp phần schema (từ đầu file tới `-- RETRACK_SCHEMA_END`, thêm COMMIT), KHÔNG chạy seed demo.
# Khối schema dùng IF NOT EXISTS nên chạy lại nhiều lần vẫn an toàn; lỗi bất kỳ ⇒ psql dừng và cả transaction bị huỷ.
# Không tự chạy trong CD theo quy ước AGENTS.md (không migrate tự động lên DB dùng chung).
set -euo pipefail
cd "$(dirname "$0")"

[ -f .env.production ] || { echo "Thiếu $(pwd)/.env.production"; exit 1; }
[ -f db/retrack-system.sql ] || { echo "Thiếu db/retrack-system.sql — chạy CD hoặc chép file từ repo trước"; exit 1; }
grep -q -- "-- RETRACK_SCHEMA_END" db/retrack-system.sql || { echo "Không thấy mốc RETRACK_SCHEMA_END trong file SQL"; exit 1; }

envval() { grep -E "^$1=" .env.production | tail -1 | cut -d= -f2- || true; }
POSTGRES_USER=$(envval POSTGRES_USER); POSTGRES_USER=${POSTGRES_USER:-retrack}
POSTGRES_DB=$(envval POSTGRES_DB); POSTGRES_DB=${POSTGRES_DB:-retrack}
COMPOSE=(docker compose --env-file .env.production -f docker-compose.prod.yml)
psql_db() { "${COMPOSE[@]}" exec -T -e PGCLIENTENCODING=UTF8 db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 "$@"; }

# Cột của lịch thu gom theo ngày (PR #87). Thiếu các cột này thì mọi API đọc pickup_requests trả 500.
check() {
  psql_db -Atc "select string_agg(c, ',') from unnest(array['collection_date','preferred_end_datetime','requires_owner_review','dispatch_review_reason']) c
    where not exists (select 1 from information_schema.columns where table_schema='public' and table_name='pickup_requests' and column_name=c)"
}

MISSING=$(check)
if [ -z "$MISSING" ]; then echo "pickup_requests đã đủ cột lịch thu gom."; else echo "pickup_requests còn thiếu cột: $MISSING"; fi
[ "${CHECK_ONLY:-0}" = 1 ] && exit 0

TS=$(date +%Y%m%d-%H%M%S)
mkdir -p backups
"${COMPOSE[@]}" exec -T db pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB" > "backups/pre-schema-$TS.dump"
[ -s "backups/pre-schema-$TS.dump" ] || { echo "Sao lưu rỗng — dừng, chưa đổi schema"; rm -f "backups/pre-schema-$TS.dump"; exit 1; }
echo "Đã sao lưu DB: backups/pre-schema-$TS.dump"

{ sed '/^-- RETRACK_SCHEMA_END/q' db/retrack-system.sql; echo 'COMMIT;'; } | psql_db -q
echo "Đã áp khối schema."

MISSING=$(check)
[ -z "$MISSING" ] || { echo "Sau khi áp vẫn thiếu cột: $MISSING"; exit 1; }
echo "Kiểm tra xong: pickup_requests đã đủ cột. Khởi động lại API: ${COMPOSE[*]} restart api"
