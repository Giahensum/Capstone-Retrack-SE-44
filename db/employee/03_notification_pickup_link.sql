-- Chạy một lần trên DB hiện có trước khi khởi động backend mới. Có thể chạy lại.
-- Không xóa thông báo, đơn thu gom hoặc thay đổi trạng thái nghiệp vụ.
BEGIN;
ALTER TABLE notifications ADD COLUMN IF NOT EXISTS pickup_request_id UUID;

-- Chỉ nối thông báo cũ đúng mẫu của workflow với đơn do chính người nhận phụ trách.
-- Không suy đoán mã đơn từ địa chỉ hoặc tên người bán.
UPDATE notifications n
SET pickup_request_id = p.id
FROM pickup_requests p
WHERE n.pickup_request_id IS NULL
  AND n.user_id = p.accepted_collector_id
  AND n.title IN ('Đã gửi kết quả cân', 'Đã mở lại kết quả cân', 'Đã bàn giao cho chủ kho')
  AND n.message LIKE 'Đơn ' || p.id::text || ' · phiên bản %';
COMMIT;
