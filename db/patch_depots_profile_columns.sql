-- Sửa lỗi 42703: column d0.contact_phone does not exist khi tải hồ sơ nhân viên.
-- Chọn đúng database mà backend đang kết nối trong pgAdmin, chạy toàn bộ file.
-- Đồng bộ đúng ba cột đã có trong db/depot ower/retrack-system.sql.
-- Không xóa/reset dữ liệu, không tạo kho/tài khoản hoặc thay cấu hình kết nối.
-- Có thể chạy lại; các cột có sẵn được giữ nguyên.
BEGIN;

ALTER TABLE depots ADD COLUMN IF NOT EXISTS contact_phone VARCHAR(20);
ALTER TABLE depots ADD COLUMN IF NOT EXISTS tax_code TEXT;
ALTER TABLE depots ADD COLUMN IF NOT EXISTS description TEXT;

COMMIT;

-- Kiểm tra database/schema và ba cột sau khi chạy.
SELECT current_database() AS database_name, current_schema() AS schema_name;
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_schema = current_schema() AND table_name = 'depots'
  AND column_name IN ('contact_phone', 'tax_code', 'description')
ORDER BY column_name;
