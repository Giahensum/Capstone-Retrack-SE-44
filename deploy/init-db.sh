#!/bin/sh
# Chỉ chạy thủ công cho database container mới, không nâng cấp DB đang có dữ liệu.
set -eu
export PGPASSWORD="$POSTGRES_PASSWORD"
tables=$(psql -X -w -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc \
    "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'")
if [ "$tables" != "0" ]; then
    echo "Database đã có bảng. Dừng khởi tạo; hãy review SQL nâng cấp riêng." >&2
    exit 1
fi
test -s /opt/retrack/bootstrap.sql
# Chỉ dùng khối schema từ file gốc; đóng transaction trước phần seed demo.
grep -q '^-- RETRACK_SCHEMA_END' /opt/retrack/bootstrap.sql
schema_file=$(mktemp)
trap 'rm -f "$schema_file"' EXIT
awk '/^-- RETRACK_SCHEMA_END/ { exit } { print }' /opt/retrack/bootstrap.sql \
    | sed "s/SET LOCAL retrack.seed_demo = 'on';/SET LOCAL retrack.seed_demo = 'off';/" > "$schema_file"
printf '\nCOMMIT;\n' >> "$schema_file"
psql -X -w -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f "$schema_file"
