-- UC-51/52/53: bổ sung bằng chứng check-in và phiên bản bản nháp.
-- Chạy một lần trên database ReTrack đã có schema; có thể chạy lại an toàn.
-- Không xóa dữ liệu, không thay đổi trạng thái/tiền của các đơn cũ.
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
COMMIT;
