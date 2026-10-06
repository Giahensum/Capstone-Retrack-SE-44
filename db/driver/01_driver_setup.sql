-- Chạy trên database ứng dụng trước khi dùng API nhánh drv-job-pool.
-- Giữ dữ liệu hiện có; liên kết thông báo có thể còn lại khi chuyến đã bị xóa.
BEGIN;
ALTER TABLE notifications ADD COLUMN IF NOT EXISTS transport_job_id UUID;
CREATE INDEX IF NOT EXISTS ix_notifications_user_created ON notifications(user_id, created_at DESC, id);

-- Older development databases may not yet contain partnership block flags.
-- The driver seed and factory-selection rules read these flags, so add them
-- idempotently without changing existing partnership rows.
ALTER TABLE factory_depot_partnerships
    ADD COLUMN IF NOT EXISTS blocked_by_depot BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS blocked_by_factory BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE inventory_batches
    ADD COLUMN IF NOT EXISTS code VARCHAR(40);
COMMIT;
