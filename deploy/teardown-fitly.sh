#!/usr/bin/env bash
# GỠ hệ thống FITLY khỏi VPS (chạy TRÊN VPS, quyền root). LUÔN sao lưu trước, hỏi xác nhận, rồi mới dừng/xoá.
#   bash teardown-fitly.sh                       # sao lưu → dừng và xoá container FITLY (GIỮ volume dữ liệu + thư mục /opt/fitly)
#   bash teardown-fitly.sh --purge-volumes       # như trên + XOÁ HẲN volume DB/Redis của FITLY (không thể khôi phục nếu mất file sao lưu!)
#   bash teardown-fitly.sh --prune-images        # thêm: xoá image Docker không còn container nào dùng (giải phóng ổ đĩa)
# File sao lưu: /root/backups/fitly-final-<giờ>.dump (pg_dump -Fc) và fitly-env-<giờ>.env (chứa secret, quyền 600).
# Khôi phục DB về sau:  pg_restore -U ecofashion -d ecofashion --clean --if-exists <file.dump>  (vào container pgvector/pg16).
# Script KHÔNG xoá ảnh trên Cloudinary và KHÔNG đụng tới stack khác trên máy.
set -euo pipefail

FITLY_DIR=${FITLY_DIR:-/opt/fitly/eco_backend/deploy}
BACKUP_DIR=${BACKUP_DIR:-/root/backups}
PURGE_VOLUMES=0
PRUNE_IMAGES=0
for a in "$@"; do
  case "$a" in
    --purge-volumes) PURGE_VOLUMES=1 ;;
    --prune-images) PRUNE_IMAGES=1 ;;
    *) echo "Tham số không hợp lệ: $a"; exit 2 ;;
  esac
done

[ "$(id -u)" -eq 0 ] || { echo "Cần chạy bằng root"; exit 1; }
[ -d "$FITLY_DIR" ] || { echo "Không thấy thư mục FITLY: $FITLY_DIR (đặt FITLY_DIR=... nếu bạn cài ở chỗ khác)"; exit 1; }
cd "$FITLY_DIR"
[ -f .env.production ] || { echo "Thiếu $FITLY_DIR/.env.production"; exit 1; }
COMPOSE=(docker compose --env-file .env.production -f docker-compose.prod.yml)

echo "== Container FITLY hiện tại =="
"${COMPOSE[@]}" ps || true
echo

TS=$(date +%Y%m%d-%H%M%S)
mkdir -p "$BACKUP_DIR"; chmod 700 "$BACKUP_DIR"
DB_FILE="$BACKUP_DIR/fitly-final-$TS.dump"
ENV_FILE="$BACKUP_DIR/fitly-env-$TS.env"

echo "== Sao lưu =="
"${COMPOSE[@]}" exec -T db pg_dump -U ecofashion -Fc ecofashion > "$DB_FILE" || { echo "pg_dump lỗi — DỪNG, chưa xoá gì. Container db có đang chạy không? (docker compose ... up -d db)"; rm -f "$DB_FILE"; exit 1; }
[ -s "$DB_FILE" ] || { echo "File sao lưu rỗng — DỪNG, chưa xoá gì"; rm -f "$DB_FILE"; exit 1; }
cp .env.production "$ENV_FILE"; chmod 600 "$ENV_FILE" "$DB_FILE"
echo "Đã lưu: $DB_FILE ($(du -h "$DB_FILE" | cut -f1)) và $ENV_FILE"
echo "HÃY TẢI hai file này về máy bạn trước khi tiếp tục nếu muốn giữ lâu dài (VPS chỉ có 20 GB và sắp được dùng cho dự án khác)."
echo

echo "Sắp làm: dừng + xoá container FITLY$( [ "$PURGE_VOLUMES" -eq 1 ] && echo ' và XOÁ HẲN volume dữ liệu' )$( [ "$PRUNE_IMAGES" -eq 1 ] && echo ' và xoá image không dùng' )."
read -r -p "Gõ đúng chữ XOA-FITLY để tiếp tục: " ans
[ "$ans" = "XOA-FITLY" ] || { echo "Đã huỷ, không thay đổi gì."; exit 1; }

DOWN=(down --remove-orphans)
[ "$PURGE_VOLUMES" -eq 1 ] && DOWN+=(-v)
"${COMPOSE[@]}" --profile visual-search --profile demo-data "${DOWN[@]}"
[ "$PRUNE_IMAGES" -eq 1 ] && docker image prune -af

echo
echo "== Cổng 80/443/4000 sau khi gỡ (nên trống để RETRACK/Caddy dùng) =="
ss -ltnp 2>/dev/null | grep -E ':(80|443|4000)\b' || echo "(trống)"
echo
echo "Xong. Lưu ý: tắt workflow Deploy trên GitHub của FITLY hoặc xoá secret SSH của nó, nếu không lần push tiếp theo sẽ DỰNG LẠI FITLY lên VPS này."
