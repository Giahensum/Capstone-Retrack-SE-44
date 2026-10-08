-- UC-64..67: chạy trên DB phát triển trước khi chạy backend mới.
-- Không tạo pickup, không thay đổi lô/chuyến hiện có. Có thể chạy lại.
BEGIN;
CREATE TABLE IF NOT EXISTS driver_delivery_events (
    id UUID PRIMARY KEY,
    job_id UUID NOT NULL REFERENCES transport_jobs(id) ON DELETE RESTRICT,
    driver_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    operation_id UUID NOT NULL,
    action VARCHAR(30) NOT NULL CHECK (action IN ('checkin','start','checkout','cancel','reject','incident')),
    reason VARCHAR(1000),
    image_url TEXT,
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    accuracy_meters DOUBLE PRECISION,
    distance_meters DOUBLE PRECISION,
    location_recorded_at TIMESTAMPTZ,
    photo_taken_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT ck_driver_reason CHECK (action NOT IN ('cancel','reject','incident') OR
        (reason IS NOT NULL AND char_length(btrim(reason)) BETWEEN 10 AND 1000)),
    CONSTRAINT ck_driver_evidence CHECK (action NOT IN ('checkin','checkout') OR
        (image_url IS NOT NULL AND latitude IS NOT NULL AND longitude IS NOT NULL
         AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180
         AND accuracy_meters IS NOT NULL AND accuracy_meters BETWEEN 0 AND 50
         AND distance_meters IS NOT NULL AND distance_meters BETWEEN 0 AND 200
         AND location_recorded_at IS NOT NULL AND photo_taken_at IS NOT NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_driver_delivery_operation
    ON driver_delivery_events(job_id, driver_id, operation_id);
CREATE INDEX IF NOT EXISTS ix_driver_delivery_history
    ON driver_delivery_events(job_id, driver_id, created_at DESC);
COMMIT;
