-- Bản vá DB-first tối thiểu cho Auth và Factory trên database cũ.
-- Chạy trên bản sao để diễn tập trước; không chạy chung với EF migration.
-- Không xóa bảng/dữ liệu, có thể chạy lại.

BEGIN;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

DO $$
BEGIN
    IF to_regclass('public.users') IS NULL OR to_regclass('public.factories') IS NULL
       OR to_regclass('public.depots') IS NULL OR to_regclass('public.inventory_batches') IS NULL
       OR to_regclass('public.factory_depot_partnerships') IS NULL THEN
        RAISE EXCEPTION 'Thiếu bảng gốc; dừng để đối chiếu schema trước khi nâng cấp';
    END IF;
END $$;

ALTER TABLE public.users ADD COLUMN IF NOT EXISTS avatar_url VARCHAR(2048);

ALTER TABLE public.depots
    ADD COLUMN IF NOT EXISTS contact_phone VARCHAR(20),
    ADD COLUMN IF NOT EXISTS tax_code TEXT,
    ADD COLUMN IF NOT EXISTS description TEXT;

ALTER TABLE public.factory_depot_partnerships
    ADD COLUMN IF NOT EXISTS blocked_by_depot BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS blocked_by_factory BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE public.inventory_batches
    ADD COLUMN IF NOT EXISTS code VARCHAR(40),
    ADD COLUMN IF NOT EXISTS image_urls TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[];
CREATE SEQUENCE IF NOT EXISTS public.depot_batch_number START WITH 1 INCREMENT BY 1;
CREATE UNIQUE INDEX IF NOT EXISTS ix_inventory_batches_code ON public.inventory_batches(code);

-- Factory nhận lô sẽ phát thông báo cho tài xế; đây là bảng thông báo dùng chung.
CREATE TABLE IF NOT EXISTS public.notifications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    message TEXT,
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    transport_job_id UUID,
    pickup_request_id UUID
);
ALTER TABLE public.notifications
    ADD COLUMN IF NOT EXISTS transport_job_id UUID,
    ADD COLUMN IF NOT EXISTS pickup_request_id UUID;
CREATE INDEX IF NOT EXISTS ix_notifications_user_created
    ON public.notifications(user_id, created_at DESC, id);

COMMIT;
