-- ============================================================
-- ReTrack — schema PostgreSQL đầy đủ cho toàn hệ thống
-- Nguồn cấu trúc cơ sở dữ liệu duy nhất (DB-first)
-- Chạy: psql -U postgres -d <database> -f "db/depot ower/retrack-system.sql"
-- ============================================================

-- Tạo database (chạy riêng nếu cần)
-- CREATE DATABASE "ReTrack_DB";
-- \c "ReTrack_DB";

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ==========================================
-- MODULE 1: HỆ THỐNG & TÀI KHOẢN (ROLES)
-- ==========================================

-- Bảng cấu hình hệ thống (Admin quản lý phí)
CREATE TABLE IF NOT EXISTS system_configs (
    config_key      VARCHAR(50)  PRIMARY KEY,
    config_value    TEXT         NOT NULL,
    description     TEXT,
    updated_at      TIMESTAMPTZ  DEFAULT NOW()
);

-- Insert mặc định
INSERT INTO system_configs (config_key, config_value, description)
VALUES ('PLATFORM_FEE_PERCENTAGE', '5.00', 'Phí nền tảng mặc định 5%')
ON CONFLICT (config_key) DO NOTHING;
UPDATE system_configs SET config_value = '5.00', description = 'Phí nền tảng mặc định 5%'
WHERE config_key = 'PLATFORM_FEE_PERCENTAGE' AND config_value = '1.00';

-- Bảng người dùng
CREATE TABLE IF NOT EXISTS users (
    id              UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    email           VARCHAR(255) UNIQUE NOT NULL,
    password_hash   TEXT         NOT NULL,
    role            VARCHAR(50)  NOT NULL,   -- SELLER, DEPOT_OWNER, DEPOT_EMPLOYEE, DRIVER, FACTORY, ADMIN
    full_name       VARCHAR(255) NOT NULL,
    phone           VARCHAR(20)  NOT NULL,
    avatar_url      VARCHAR(2048),
    is_active       BOOLEAN      DEFAULT TRUE,
    created_at      TIMESTAMPTZ  DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  DEFAULT NOW()
);

-- Bảng kho/depot
CREATE TABLE IF NOT EXISTS depots (
    id              UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    owner_id        UUID         NOT NULL REFERENCES users(id),
    name            VARCHAR(255) NOT NULL,
    address         TEXT         NOT NULL,
    latitude        DECIMAL(10, 7),
    longitude       DECIMAL(10, 7),
    rating          DECIMAL(3, 2) DEFAULT 0.0,
    contact_phone   VARCHAR(20),
    tax_code        TEXT,
    description     TEXT,
    created_at      TIMESTAMPTZ  DEFAULT NOW()
);
ALTER TABLE depots ADD COLUMN IF NOT EXISTS contact_phone VARCHAR(20);
ALTER TABLE depots ADD COLUMN IF NOT EXISTS tax_code TEXT;
ALTER TABLE depots ADD COLUMN IF NOT EXISTS description TEXT;

-- Bảng nhà máy
CREATE TABLE IF NOT EXISTS factories (
    id              UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    owner_id        UUID         NOT NULL REFERENCES users(id),
    name            VARCHAR(255) NOT NULL,
    address         TEXT         NOT NULL,
    latitude        DECIMAL(10, 7),
    longitude       DECIMAL(10, 7),
    rating          DECIMAL(3, 2) DEFAULT 0.0,
    tax_code        VARCHAR(50),
    industrial_zone VARCHAR(200),
    contact_phone   VARCHAR(30),
    business_license_url TEXT,
    environmental_license_url TEXT,
    capacity_kg_per_month DECIMAL(18, 2) NOT NULL DEFAULT 0,
    minimum_purity_percent DECIMAL(5, 2) NOT NULL DEFAULT 0,
    accepted_materials TEXT NOT NULL DEFAULT 'PET',
    created_at      TIMESTAMPTZ  DEFAULT NOW()
);

-- Nhân viên & Tài xế thuộc về 1 Depot
CREATE TABLE IF NOT EXISTS depot_staffs (
    id              UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    depot_id        UUID         NOT NULL REFERENCES depots(id),
    user_id         UUID         NOT NULL REFERENCES users(id),
    staff_type      VARCHAR(50)  NOT NULL,   -- DEPOT_EMPLOYEE, DRIVER
    is_active       BOOLEAN      DEFAULT TRUE
);

-- ==========================================
-- MODULE 2: GIAI ĐOẠN 1 - THU GOM PHẾ LIỆU & PHÍ MẶC ĐỊNH 5%
-- ==========================================

-- Yêu cầu thu gom của Seller
CREATE TABLE IF NOT EXISTS pickup_requests (
    id                       UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    seller_id                UUID         NOT NULL REFERENCES users(id),
    target_depot_id          UUID         REFERENCES depots(id),
    accepted_collector_id    UUID         REFERENCES users(id),
    description              TEXT,
    request_image_url        TEXT,
    address                  TEXT         NOT NULL,
    latitude                 DECIMAL(10, 7),
    longitude                DECIMAL(10, 7),
    preferred_datetime       TIMESTAMPTZ,
    checkin_image_url        TEXT,
    -- Hạch toán tài chính (mức phí mặc định 5%, lưu riêng theo từng giao dịch)
    gross_amount             DECIMAL(18, 2) DEFAULT 0,
    platform_fee_percentage  DECIMAL(5, 2)  DEFAULT 0,
    platform_fee_amount      DECIMAL(18, 2) DEFAULT 0,
    net_amount               DECIMAL(18, 2) DEFAULT 0,
    payment_proof_url        TEXT,
    status                   VARCHAR(50)  NOT NULL DEFAULT 'PENDING',
    -- PENDING, SCHEDULED, WEIGHED, SELLER_CONFIRMED, AWAITING_PAYMENT, PAYMENT_SENT, DONE
    created_at               TIMESTAMPTZ  DEFAULT NOW(),
    updated_at               TIMESTAMPTZ  DEFAULT NOW()
);

-- Bằng chứng UC-51 và phiên bản bản nháp UC-52/53.
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

-- Chi tiết các loại phế liệu (NV cân và nhập)
CREATE TABLE IF NOT EXISTS pickup_request_items (
    id                  UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    pickup_request_id   UUID         NOT NULL REFERENCES pickup_requests(id) ON DELETE CASCADE,
    material_type       VARCHAR(100) NOT NULL,
    weight_kg           DECIMAL(10, 2) NOT NULL,
    price_per_kg        DECIMAL(18, 2) NOT NULL,
    sub_total           DECIMAL(18, 2) NOT NULL
);

-- Đánh giá Seller dành cho Depot
CREATE TABLE IF NOT EXISTS seller_depot_reviews (
    id                  UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    pickup_request_id   UUID         NOT NULL REFERENCES pickup_requests(id),
    depot_id            UUID         NOT NULL REFERENCES depots(id),
    rating              INT          CHECK (rating >= 1 AND rating <= 5),
    comment             TEXT,
    created_at          TIMESTAMPTZ  DEFAULT NOW()
);

-- ==========================================
-- MODULE 3: DEMAND BOARD & LÔ HÀNG (GIAI ĐOẠN 2)
-- ==========================================

-- Bảng nhu cầu nhà máy (Demand Board)
CREATE TABLE IF NOT EXISTS factory_demands (
    id                  UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    factory_id          UUID         NOT NULL REFERENCES factories(id),
    material_type       VARCHAR(100) NOT NULL,
    required_weight_kg  DECIMAL(18, 2) NOT NULL,
    min_price_per_kg    DECIMAL(18, 2),
    max_price_per_kg    DECIMAL(18, 2),
    deadline            TIMESTAMPTZ  NOT NULL,
    is_active           BOOLEAN      DEFAULT TRUE,
    note                TEXT,
    created_at          TIMESTAMPTZ  DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  DEFAULT NOW()
);

-- Quan hệ đối tác Depot - Factory
CREATE TABLE IF NOT EXISTS factory_depot_partnerships (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    depot_id    UUID        NOT NULL REFERENCES depots(id),
    factory_id  UUID        NOT NULL REFERENCES factories(id),
    status      VARCHAR(50) NOT NULL DEFAULT 'PENDING',   -- PENDING, APPROVED, BLOCKED
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW(),
    blocked_by_depot BOOLEAN NOT NULL DEFAULT FALSE,
    blocked_by_factory BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT uq_factory_depot UNIQUE (depot_id, factory_id)
);
ALTER TABLE factory_depot_partnerships ADD COLUMN IF NOT EXISTS blocked_by_depot BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE factory_depot_partnerships ADD COLUMN IF NOT EXISTS blocked_by_factory BOOLEAN NOT NULL DEFAULT FALSE;

-- Lô hàng tồn kho
CREATE TABLE IF NOT EXISTS inventory_batches (
    id                  UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    depot_id            UUID         NOT NULL REFERENCES depots(id),
    target_factory_id   UUID         REFERENCES factories(id),
    direct_offer_factory_id UUID     REFERENCES factories(id),
    material_type       VARCHAR(100) NOT NULL,
    declared_weight_kg  DECIMAL(18, 2) NOT NULL,
    description         TEXT,
    status              VARCHAR(50)  NOT NULL,
    actual_weight_kg    DECIMAL(18, 2),
    factory_received_at TIMESTAMPTZ,
    factory_decided_at  TIMESTAMPTZ,
    rejection_reason    TEXT,
    agreed_price_per_kg DECIMAL(18, 2),
    gross_amount        DECIMAL(18, 2),
    platform_fee_amount DECIMAL(18, 2),
    net_amount          DECIMAL(18, 2),
    payment_reference   VARCHAR(200),
    settled_at          TIMESTAMPTZ,
    code                VARCHAR(40),
    image_urls          TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[],
    -- MARKETPLACE, PENDING_APPROVAL, TRANSPORT_READY, COMPLETED
    created_at          TIMESTAMPTZ  DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  DEFAULT NOW()
);
ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS code VARCHAR(40);
ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS image_urls TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[];
CREATE SEQUENCE IF NOT EXISTS depot_batch_number START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
CREATE UNIQUE INDEX IF NOT EXISTS ix_inventory_batches_code ON inventory_batches(code);

-- ==========================================
-- MODULE 4: VẬN CHUYỂN & QC & QUYẾT TOÁN CÓ PHÍ (GIAI ĐOẠN 3)
-- ==========================================

-- Công việc vận chuyển
CREATE TABLE IF NOT EXISTS transport_jobs (
    id                          UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    batch_id                    UUID        NOT NULL UNIQUE REFERENCES inventory_batches(id),
    driver_id                   UUID        REFERENCES users(id),
    status                      VARCHAR(50) NOT NULL DEFAULT 'PENDING',  -- PENDING, IN_TRANSIT, DELIVERED
    checkin_depot_image_url     TEXT,
    checkout_factory_image_url  TEXT,
    created_at                  TIMESTAMPTZ DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ DEFAULT NOW()
);

-- Bảng giá tham khảo. Giá trong ứng dụng phải được Admin cập nhật kèm nguồn.
CREATE TABLE IF NOT EXISTS market_prices (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    material_type   VARCHAR(100) NOT NULL,
    price_per_kg    DECIMAL(18, 2) NOT NULL CHECK (price_per_kg > 0),
    effective_date  TIMESTAMPTZ NOT NULL,
    source          TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Kiểm tra chất lượng tại nhà máy
CREATE TABLE IF NOT EXISTS batch_quality_checks (
    id                      UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    batch_id                UUID         NOT NULL UNIQUE REFERENCES inventory_batches(id),
    factory_id              UUID         NOT NULL REFERENCES factories(id),
    actual_weight_kg        DECIMAL(18, 2) NOT NULL,
    grade                   VARCHAR(10)  NOT NULL,
    agreed_price_per_kg     DECIMAL(18, 2) NOT NULL,
    -- Hạch toán tài chính (mức phí mặc định 5%, lưu riêng theo từng giao dịch)
    gross_amount            DECIMAL(18, 2) NOT NULL,
    platform_fee_percentage DECIMAL(5, 2)  DEFAULT 0,
    platform_fee_amount     DECIMAL(18, 2) DEFAULT 0,
    net_amount              DECIMAL(18, 2) NOT NULL,
    payment_proof_url       TEXT,
    is_accepted             BOOLEAN      NOT NULL,
    gross_weight_kg         DECIMAL(18, 2),
    tare_weight_kg          DECIMAL(18, 2),
    difference_percentage  DECIMAL(8, 2),
    ticket_number           VARCHAR(100),
    ticket_image_url        TEXT,
    purity_percent          DECIMAL(5, 2),
    moisture_percent        DECIMAL(5, 2),
    contamination_percent   DECIMAL(5, 2),
    quality_note            TEXT,
    resolution              VARCHAR(20),
    invoice_number          VARCHAR(100),
    invoice_file_url        TEXT,
    invoice_status          VARCHAR(30),
    created_at              TIMESTAMPTZ  DEFAULT NOW()
);

-- Factory đánh giá Depot
CREATE TABLE IF NOT EXISTS factory_depot_reviews (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    batch_id    UUID        NOT NULL REFERENCES inventory_batches(id),
    factory_id  UUID        NOT NULL REFERENCES factories(id),
    depot_id    UUID        NOT NULL REFERENCES depots(id),
    rating      INT         CHECK (rating >= 1 AND rating <= 5),
    comment     TEXT,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- ==========================================
-- MODULE 5: REVENUE - DOANH THU NỀN TẢNG
-- ==========================================

-- Bảng lưu vết doanh thu của nền tảng để hiển thị cho Admin
CREATE TABLE IF NOT EXISTS platform_transactions (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    source_type VARCHAR(50) NOT NULL,  -- 'PICKUP_REQUEST' hoặc 'BATCH_ORDER'
    source_id   UUID        NOT NULL,  -- ID của pickup_requests hoặc inventory_batches
    fee_amount  DECIMAL(18, 2) NOT NULL,
    payer_id    UUID,                  -- Người trả phí, nếu giao dịch cũ có thông tin này
    transaction_amount DECIMAL(18, 2), -- Giá trị giao dịch gốc, nếu có
    fee_percentage DECIMAL(5, 2),      -- Tỷ lệ phí đã áp dụng, nếu có
    description TEXT,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);
ALTER TABLE platform_transactions ADD COLUMN IF NOT EXISTS payer_id UUID;
ALTER TABLE platform_transactions ADD COLUMN IF NOT EXISTS transaction_amount DECIMAL(18, 2);
ALTER TABLE platform_transactions ADD COLUMN IF NOT EXISTS fee_percentage DECIMAL(5, 2);

-- ==========================================
-- MODULE 6: ADMIN - AUDIT LOG, HÓA ĐƠN PHÍ, THÔNG BÁO
-- ==========================================

-- Nhật ký hành động của Admin trên hệ thống (tạo/sửa/xóa user, giá tham khảo, cấu hình phí, hóa đơn...)
CREATE TABLE IF NOT EXISTS audit_logs (
    id          UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id     UUID         REFERENCES users(id) ON DELETE SET NULL,  -- NULL nếu user bị xóa, log vẫn giữ lại
    action      VARCHAR(100) NOT NULL,   -- CREATE, UPDATE, DELETE, ACTIVATE, DEACTIVATE, MARK_PAID, GENERATE...
    entity_name VARCHAR(100) NOT NULL,   -- User, MarketPrice, SystemConfig, PlatformInvoice...
    entity_id   UUID,
    old_data    TEXT,   -- JSON snapshot trước khi đổi
    new_data    TEXT,   -- JSON snapshot sau khi đổi
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Thông báo trong hệ thống (ví dụ: nhắc thanh toán hóa đơn phí nền tảng)
CREATE TABLE IF NOT EXISTS notifications (
    id          UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title       VARCHAR(255) NOT NULL,
    message     TEXT,
    is_read     BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Hóa đơn phí nền tảng hàng tháng, gộp theo từng Depot Owner / Factory có phát sinh giao dịch
ALTER TABLE notifications ADD COLUMN IF NOT EXISTS transport_job_id UUID;
ALTER TABLE notifications ADD COLUMN IF NOT EXISTS pickup_request_id UUID;
CREATE INDEX IF NOT EXISTS ix_notifications_user_created ON notifications(user_id, created_at DESC, id);

CREATE TABLE IF NOT EXISTS platform_invoices (
    id               UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    payer_id         UUID         NOT NULL REFERENCES users(id),
    period_year      INT          NOT NULL,
    period_month     INT          NOT NULL,
    total_fee_amount DECIMAL(18, 2) NOT NULL,
    status           VARCHAR(20)  NOT NULL DEFAULT 'PENDING',  -- PENDING, PAID
    paid_at          TIMESTAMPTZ,
    payment_proof_url TEXT,
    submitted_at     TIMESTAMPTZ,
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_platform_invoice_period UNIQUE (payer_id, period_year, period_month)
);
ALTER TABLE platform_invoices ADD COLUMN IF NOT EXISTS payment_proof_url TEXT;
ALTER TABLE platform_invoices ADD COLUMN IF NOT EXISTS submitted_at TIMESTAMPTZ;

-- ==========================================
-- FUNCTIONS & TRIGGERS (updated_at tự động)
-- ==========================================

-- Function dùng chung cho trigger updated_at
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger cho system_configs
DROP TRIGGER IF EXISTS trg_system_configs_upd ON system_configs;
CREATE TRIGGER trg_system_configs_upd
    BEFORE UPDATE ON system_configs
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho users
DROP TRIGGER IF EXISTS trg_users_upd ON users;
CREATE TRIGGER trg_users_upd
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho pickup_requests
DROP TRIGGER IF EXISTS trg_pickup_requests_upd ON pickup_requests;
CREATE TRIGGER trg_pickup_requests_upd
    BEFORE UPDATE ON pickup_requests
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho inventory_batches
DROP TRIGGER IF EXISTS trg_inventory_batches_upd ON inventory_batches;
CREATE TRIGGER trg_inventory_batches_upd
    BEFORE UPDATE ON inventory_batches
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho transport_jobs
DROP TRIGGER IF EXISTS trg_transport_jobs_upd ON transport_jobs;
CREATE TRIGGER trg_transport_jobs_upd
    BEFORE UPDATE ON transport_jobs
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho factory_depot_partnerships
DROP TRIGGER IF EXISTS trg_partnerships_upd ON factory_depot_partnerships;
CREATE TRIGGER trg_partnerships_upd
    BEFORE UPDATE ON factory_depot_partnerships
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ==========================================
-- INDEX GỢI Ý (Performance)
-- ==========================================
CREATE INDEX IF NOT EXISTS idx_users_email     ON users(email);
CREATE INDEX IF NOT EXISTS idx_users_role      ON users(role);
CREATE INDEX IF NOT EXISTS idx_pr_seller       ON pickup_requests(seller_id);
CREATE INDEX IF NOT EXISTS idx_pr_status       ON pickup_requests(status);
CREATE INDEX IF NOT EXISTS idx_pr_depot        ON pickup_requests(target_depot_id);
CREATE INDEX IF NOT EXISTS idx_ib_depot        ON inventory_batches(depot_id);
CREATE INDEX IF NOT EXISTS idx_ib_status       ON inventory_batches(status);
CREATE INDEX IF NOT EXISTS idx_tj_driver       ON transport_jobs(driver_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_platform_transactions_source ON platform_transactions(source_type, source_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user     ON audit_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_entity   ON audit_logs(entity_name);
CREATE INDEX IF NOT EXISTS idx_notifications_user  ON notifications(user_id);
CREATE INDEX IF NOT EXISTS idx_pi_payer            ON platform_invoices(payer_id);
CREATE INDEX IF NOT EXISTS idx_pi_status           ON platform_invoices(status);

-- Employee: lịch sử gửi kết quả cân và bàn giao chủ kho.
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
