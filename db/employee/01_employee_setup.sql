-- Chuẩn bị database Employee trên schema ReTrack hiện có. Chạy toàn bộ file trong pgAdmin.
-- Không xóa dữ liệu. Đồng bộ cột hồ sơ kho, check-in, nhật ký và cấu hình phí 5%.
-- Cấu hình phí dùng chung toàn hệ thống; không sửa phí đã lưu trên các đơn cũ.
BEGIN;
CREATE TABLE IF NOT EXISTS pickup_checkins (
    pickup_request_id UUID PRIMARY KEY REFERENCES pickup_requests(id) ON DELETE CASCADE,
    employee_id UUID NOT NULL REFERENCES users(id),
    latitude DOUBLE PRECISION NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude DOUBLE PRECISION NOT NULL CHECK (longitude BETWEEN -180 AND 180),
    accuracy_meters DOUBLE PRECISION NOT NULL CHECK (accuracy_meters BETWEEN 0 AND 50),
    distance_meters DOUBLE PRECISION NOT NULL CHECK (distance_meters BETWEEN 0 AND 200),
    location_recorded_at TIMESTAMPTZ NOT NULL,
    photo_taken_at TIMESTAMPTZ NOT NULL,
    checked_in_at TIMESTAMPTZ NOT NULL,
    image_url TEXT NOT NULL,
    revision INTEGER NOT NULL DEFAULT 0 CHECK (revision >= 0)
);

CREATE TABLE IF NOT EXISTS employee_collection_events (
    id UUID PRIMARY KEY,
    pickup_request_id UUID NOT NULL REFERENCES pickup_requests(id) ON DELETE CASCADE,
    employee_id UUID NOT NULL REFERENCES users(id),
    kind VARCHAR(30) NOT NULL CHECK (kind IN ('SUBMITTED', 'HANDED_OVER', 'REOPENED')),
    revision INTEGER NOT NULL CHECK (revision >= 0),
    from_status VARCHAR(30) NOT NULL,
    to_status VARCHAR(30) NOT NULL,
    snapshot_json TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    UNIQUE (pickup_request_id, kind, revision)
);
CREATE INDEX IF NOT EXISTS ix_employee_collection_events_employee_time
    ON employee_collection_events(employee_id, created_at DESC);

ALTER TABLE depots ADD COLUMN IF NOT EXISTS contact_phone VARCHAR(20);
ALTER TABLE depots ADD COLUMN IF NOT EXISTS tax_code TEXT;
ALTER TABLE depots ADD COLUMN IF NOT EXISTS description TEXT;

INSERT INTO system_configs(config_key, config_value, description, updated_at)
VALUES ('PLATFORM_FEE_PERCENTAGE', '5.00', 'Phí nền tảng mặc định 5%', NOW())
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, description = EXCLUDED.description, updated_at = EXCLUDED.updated_at;
COMMIT;
SELECT current_database(), to_regclass('pickup_checkins'), to_regclass('employee_collection_events');
