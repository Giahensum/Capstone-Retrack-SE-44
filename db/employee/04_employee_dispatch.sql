-- Nâng cấp DB hiện có. Dừng backend, chọn đúng DB trong pgAdmin, chạy toàn bộ file.
-- Không seed, không đổi đơn/tài khoản, không tự áp dụng EF migration.
BEGIN;
-- RETRACK_EMPLOYEE_DISPATCH_BEGIN
CREATE TABLE IF NOT EXISTS employee_collection_areas (
    id UUID PRIMARY KEY,
    depot_id UUID NOT NULL REFERENCES depots(id),
    name VARCHAR(120) NOT NULL,
    latitude DOUBLE PRECISION NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude DOUBLE PRECISION NOT NULL CHECK (longitude BETWEEN -180 AND 180),
    radius_meters DOUBLE PRECISION NOT NULL CHECK (radius_meters BETWEEN 100 AND 20000),
    is_active BOOLEAN NOT NULL DEFAULT true
);
CREATE TABLE IF NOT EXISTS employee_dispatch_shifts (
    id UUID PRIMARY KEY,
    area_id UUID NOT NULL REFERENCES employee_collection_areas(id),
    employee_id UUID NOT NULL REFERENCES users(id),
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NOT NULL,
    availability VARCHAR(20) NOT NULL CHECK (availability IN ('AVAILABLE','FULL','STOPPED')),
    reason VARCHAR(1000),
    vehicle VARCHAR(20) NOT NULL CHECK (vehicle IN ('motorcycle','car','truck')),
    capacity_kg NUMERIC(10,2) NOT NULL CHECK (capacity_kg > 0 AND capacity_kg <= 20000),
    max_pending_orders INTEGER NOT NULL CHECK (max_pending_orders BETWEEN 1 AND 50),
    updated_at TIMESTAMPTZ NOT NULL,
    CHECK (ends_at > starts_at AND ends_at <= starts_at + interval '24 hours'),
    CHECK (availability = 'AVAILABLE' OR (reason IS NOT NULL AND char_length(btrim(reason)) BETWEEN 10 AND 1000))
);
CREATE INDEX IF NOT EXISTS ix_dispatch_shift_employee ON employee_dispatch_shifts(employee_id, starts_at, ends_at);
CREATE TABLE IF NOT EXISTS employee_dispatch_queue (
    pickup_id UUID PRIMARY KEY REFERENCES pickup_requests(id) ON DELETE CASCADE,
    reason TEXT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);
CREATE TABLE IF NOT EXISTS employee_dispatch_events (
    id UUID PRIMARY KEY,
    pickup_id UUID NOT NULL REFERENCES pickup_requests(id) ON DELETE CASCADE,
    from_employee_id UUID REFERENCES users(id),
    to_employee_id UUID REFERENCES users(id),
    shift_id UUID REFERENCES employee_dispatch_shifts(id),
    reason TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_dispatch_event_pickup ON employee_dispatch_events(pickup_id, created_at);
CREATE INDEX IF NOT EXISTS ix_dispatch_pickup_pending ON pickup_requests(status, preferred_datetime, id);

-- Ảnh vẫn bắt buộc; GPS có thể thiếu và không bị chặn vì khoảng cách/sai số.
ALTER TABLE pickup_checkins ALTER COLUMN latitude DROP NOT NULL;
ALTER TABLE pickup_checkins ALTER COLUMN longitude DROP NOT NULL;
ALTER TABLE pickup_checkins ALTER COLUMN accuracy_meters DROP NOT NULL;
ALTER TABLE pickup_checkins ALTER COLUMN distance_meters DROP NOT NULL;
ALTER TABLE pickup_checkins ALTER COLUMN location_recorded_at DROP NOT NULL;
-- Tên constraint có thể khác nhau giữa bootstrap và schema cũ: chỉ thay kiểm tra hai cột này.
DO $constraints$
DECLARE c record;
BEGIN
    FOR c IN SELECT conname FROM pg_constraint
        WHERE conrelid = 'pickup_checkins'::regclass AND contype = 'c'
          AND (pg_get_constraintdef(oid) LIKE '%accuracy_meters%' OR pg_get_constraintdef(oid) LIKE '%distance_meters%')
    LOOP EXECUTE format('ALTER TABLE pickup_checkins DROP CONSTRAINT %I', c.conname); END LOOP;
END $constraints$;
ALTER TABLE pickup_checkins ADD CONSTRAINT ck_pickup_optional_accuracy CHECK (accuracy_meters >= 0 AND accuracy_meters < 'Infinity'::float8);
ALTER TABLE pickup_checkins ADD CONSTRAINT ck_pickup_optional_distance CHECK (distance_meters >= 0 AND distance_meters < 'Infinity'::float8);
ALTER TABLE driver_delivery_events DROP CONSTRAINT IF EXISTS ck_driver_evidence;
ALTER TABLE driver_delivery_events ADD CONSTRAINT ck_driver_evidence CHECK (
    action NOT IN ('checkin','checkout') OR
    (image_url IS NOT NULL AND length(btrim(image_url)) > 0 AND photo_taken_at IS NOT NULL));
-- RETRACK_EMPLOYEE_DISPATCH_END
COMMIT;
