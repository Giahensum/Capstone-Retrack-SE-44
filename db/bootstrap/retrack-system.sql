-- ============================================================
-- RETRACK — KHỞI TẠO TOÀN HỆ THỐNG VÀ DỮ LIỆU DEMO
-- Máy mới: tạo database rỗng bằng pgAdmin, mở Query Tool của đúng DB,
-- mở file này và nhấn F5 để chạy TOÀN BỘ, không cần lệnh terminal.
-- Mặc định có seed demo các role tại Đà Nẵng. Không dùng cho production.
-- Muốn chỉ tạo cấu trúc: đổi retrack.seed_demo từ 'on' thành 'off' bên dưới.
-- Không thay kết nối/mật khẩu PostgreSQL; cấu hình backend bằng .env local.
-- File khởi tạo không thay thế quy trình nâng cấp database có dữ liệu thật.
-- Nếu lỗi: toàn bộ transaction chưa được ghi; chạy ROLLBACK; trước khi thử lại.
-- ============================================================

BEGIN;
SET LOCAL retrack.seed_demo = 'on';

-- RETRACK_SCHEMA_BEGIN
-- UUID dùng hàm lõi của PostgreSQL, không phụ thuộc extension hoặc schema cài đặt.

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
ALTER TABLE system_configs ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
ALTER TABLE system_configs ALTER COLUMN updated_at SET DEFAULT NOW();
UPDATE system_configs SET updated_at = NOW() WHERE updated_at IS NULL;

-- Insert mặc định
INSERT INTO system_configs (config_key, config_value, description)
VALUES ('PLATFORM_FEE_PERCENTAGE', '5.00', 'Phí nền tảng mặc định 5%')
ON CONFLICT (config_key) DO NOTHING;
UPDATE system_configs SET config_value = '5.00', description = 'Phí nền tảng mặc định 5%'
WHERE config_key = 'PLATFORM_FEE_PERCENTAGE' AND config_value = '1.00';

-- Bảng người dùng
CREATE TABLE IF NOT EXISTS users (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    email           VARCHAR(255) UNIQUE NOT NULL,
    password_hash   TEXT         NOT NULL,
    role            VARCHAR(50)  NOT NULL,   -- SELLER, DEPOT_OWNER, DEPOT_EMPLOYEE, DRIVER, FACTORY, ADMIN
    full_name       VARCHAR(255) NOT NULL,
    phone           VARCHAR(20)  NOT NULL,
    bank_name       VARCHAR(255),
    bank_account_number VARCHAR(100),
    bank_account_name VARCHAR(255),
    bank_qr_url     VARCHAR(2048),
    avatar_url      VARCHAR(2048),
    is_active       BOOLEAN      DEFAULT TRUE,
    created_at      TIMESTAMPTZ  DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  DEFAULT NOW()
);
ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_name VARCHAR(255);
ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_account_number VARCHAR(100);
ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_account_name VARCHAR(255);
ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_qr_url VARCHAR(2048);

-- Bảng kho/depot
CREATE TABLE IF NOT EXISTS depots (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id                       UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id                  UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    pickup_request_id   UUID         NOT NULL REFERENCES pickup_requests(id) ON DELETE CASCADE,
    material_type       VARCHAR(100) NOT NULL,
    weight_kg           DECIMAL(10, 2) NOT NULL,
    price_per_kg        DECIMAL(18, 2) NOT NULL,
    sub_total           DECIMAL(18, 2) NOT NULL
);

-- Đánh giá Seller dành cho Depot
CREATE TABLE IF NOT EXISTS seller_depot_reviews (
    id                  UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id                  UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id                  UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id                          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    material_type   VARCHAR(100) NOT NULL,
    price_per_kg    DECIMAL(18, 2) NOT NULL CHECK (price_per_kg > 0),
    effective_date  TIMESTAMPTZ NOT NULL,
    source          TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Kiểm tra chất lượng tại nhà máy
CREATE TABLE IF NOT EXISTS batch_quality_checks (
    id                      UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
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
-- Không mở/đóng transaction giữa file: bên gọi có thể chạy toàn bộ schema trong một transaction.
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

-- ==========================================
-- MODULE 8: XÁC THỰC OTP & QUÊN MẬT KHẨU
-- ==========================================
CREATE TABLE IF NOT EXISTS password_reset_otps (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email       VARCHAR(255) NOT NULL,
    otp_code    VARCHAR(10) NOT NULL,
    expires_at  TIMESTAMPTZ NOT NULL,
    is_used     BOOLEAN DEFAULT FALSE,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_password_reset_otps_email ON password_reset_otps(email);

-- RETRACK_DEPOT_STOCK_V1
-- Nhập kho đa nguồn bổ sung: giữ nguyên DONE cũ, không sao chép dữ liệu thu gom.
-- Có thể chạy lại toàn bộ file trên DB hiện có; ứng dụng không tự migrate.
CREATE TABLE IF NOT EXISTS depot_stock_receipts (
    id UUID PRIMARY KEY,
    depot_id UUID NOT NULL REFERENCES depots(id) ON DELETE RESTRICT,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    source_type VARCHAR(40) NOT NULL CHECK (source_type IN ('EXTERNAL_PURCHASE','OPENING_BALANCE','BULK_PURCHASE')),
    reference VARCHAR(255) NOT NULL CHECK (length(btrim(reference)) > 0),
    status VARCHAR(20) NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT','SUBMITTED','POSTED','REJECTED','VOIDED')),
    received_at TIMESTAMPTZ NOT NULL,
    purchase_cost NUMERIC(18,0) NOT NULL CHECK (purchase_cost >= 0),
    costs_confirmed BOOLEAN NOT NULL DEFAULT FALSE,
    request_hash VARCHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reviewed_at TIMESTAMPTZ,
    reviewed_by UUID REFERENCES users(id) ON DELETE RESTRICT,
    posted_at TIMESTAMPTZ,
    posted_by UUID REFERENCES users(id) ON DELETE RESTRICT,
    review_note VARCHAR(1000)
);
CREATE INDEX IF NOT EXISTS ix_depot_stock_receipts_scope ON depot_stock_receipts(depot_id, created_at DESC, id);
ALTER TABLE depot_stock_receipts ADD COLUMN IF NOT EXISTS posted_at TIMESTAMPTZ;
ALTER TABLE depot_stock_receipts ADD COLUMN IF NOT EXISTS posted_by UUID REFERENCES users(id) ON DELETE RESTRICT;
ALTER TABLE depot_stock_receipts DROP CONSTRAINT IF EXISTS depot_stock_receipts_status_check;
ALTER TABLE depot_stock_receipts ADD CONSTRAINT depot_stock_receipts_status_check
    CHECK (status IN ('DRAFT','SUBMITTED','POSTED','REJECTED','VOIDED'));
CREATE TABLE IF NOT EXISTS depot_stock_lines (
    id UUID PRIMARY KEY,
    receipt_id UUID NOT NULL REFERENCES depot_stock_receipts(id) ON DELETE CASCADE,
    kind VARCHAR(20) NOT NULL CHECK (kind IN ('MATERIAL','REUSABLE')),
    material_type VARCHAR(100), name VARCHAR(255) NOT NULL,
    unit VARCHAR(10) NOT NULL,
    quantity NUMERIC(18,3) NOT NULL CHECK (quantity > 0),
    reference_price NUMERIC(18,0) NOT NULL CHECK (reference_price >= 0),
    direct_cost NUMERIC(18,0) NOT NULL CHECK (direct_cost >= 0),
    allocated_cost NUMERIC(18,0) CHECK (allocated_cost >= 0),
    condition_note VARCHAR(1000), position INTEGER NOT NULL,
    UNIQUE(receipt_id, position),
    CHECK ((kind = 'MATERIAL' AND unit = 'kg' AND material_type IS NOT NULL) OR
        (kind = 'REUSABLE' AND unit IN ('cái','bộ') AND material_type IS NULL AND quantity = trunc(quantity)))
);
CREATE TABLE IF NOT EXISTS depot_stock_expenses (
    id UUID PRIMARY KEY, receipt_id UUID NOT NULL REFERENCES depot_stock_receipts(id) ON DELETE CASCADE,
    category VARCHAR(30) NOT NULL, description VARCHAR(255) NOT NULL,
    amount NUMERIC(18,0) NOT NULL CHECK (amount >= 0)
);
CREATE TABLE IF NOT EXISTS depot_reuse_sales (
    id UUID PRIMARY KEY, line_id UUID NOT NULL REFERENCES depot_stock_lines(id) ON DELETE RESTRICT,
    quantity NUMERIC(18,3) NOT NULL CHECK (quantity > 0 AND quantity = trunc(quantity)),
    unit_price NUMERIC(18,0) NOT NULL CHECK (unit_price >= 0),
    selling_cost NUMERIC(18,0) NOT NULL CHECK (selling_cost >= 0),
    note VARCHAR(1000) NOT NULL, created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_depot_reuse_sales_line ON depot_reuse_sales(line_id);
CREATE TABLE IF NOT EXISTS depot_batch_cost_sources (
    id UUID PRIMARY KEY, batch_id UUID NOT NULL REFERENCES inventory_batches(id) ON DELETE CASCADE,
    source_id UUID NOT NULL, source_type VARCHAR(20) NOT NULL CHECK (source_type IN ('PICKUP','RECEIPT')),
    source_label VARCHAR(255) NOT NULL,
    quantity NUMERIC(18,3) NOT NULL CHECK (quantity > 0), unit_cost NUMERIC(30,9) CHECK (unit_cost >= 0),
    UNIQUE(batch_id, source_type, source_id)
);
CREATE INDEX IF NOT EXISTS ix_depot_batch_cost_source ON depot_batch_cost_sources(source_type, source_id);
ALTER TABLE depot_batch_cost_sources ALTER COLUMN unit_cost TYPE NUMERIC(30,9);
CREATE TABLE IF NOT EXISTS depot_batch_expenses (
    id UUID PRIMARY KEY, batch_id UUID NOT NULL REFERENCES inventory_batches(id) ON DELETE CASCADE,
    description VARCHAR(255) NOT NULL, amount NUMERIC(18,0) NOT NULL CHECK (amount >= 0),
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_depot_batch_expenses_batch ON depot_batch_expenses(batch_id, created_at);

-- RETRACK_DEPOT_VERIFICATION_V1: hồ sơ riêng tư; không tự duyệt các kho đang có.
CREATE TABLE IF NOT EXISTS depot_stock_cost_adjustments (
    id UUID PRIMARY KEY, line_id UUID NOT NULL REFERENCES depot_stock_lines(id) ON DELETE CASCADE,
    kind VARCHAR(30) NOT NULL CHECK (kind IN ('ADDITIONAL_COST','CONFIRM_BASE_COST')),
    amount NUMERIC(18,0) NOT NULL, reason VARCHAR(1000) NOT NULL CHECK (length(btrim(reason)) > 0),
    created_by UUID NOT NULL REFERENCES users(id), created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_stock_cost_adjustments_line_date ON depot_stock_cost_adjustments(line_id, created_at, id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_stock_cost_base_once ON depot_stock_cost_adjustments(line_id) WHERE kind = 'CONFIRM_BASE_COST';
CREATE TABLE IF NOT EXISTS depot_cost_reconciliations (
    id UUID PRIMARY KEY, sequence_no BIGINT GENERATED BY DEFAULT AS IDENTITY UNIQUE,
    adjustment_id UUID NOT NULL REFERENCES depot_stock_cost_adjustments(id) ON DELETE CASCADE,
    batch_source_id UUID REFERENCES depot_batch_cost_sources(id) ON DELETE CASCADE,
    reuse_sale_id UUID REFERENCES depot_reuse_sales(id) ON DELETE CASCADE,
    unit_cost NUMERIC(30,9) CHECK (unit_cost >= 0), created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK ((batch_source_id IS NULL) <> (reuse_sale_id IS NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_cost_reconcile_source ON depot_cost_reconciliations(adjustment_id, batch_source_id) WHERE batch_source_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ix_cost_reconcile_sale ON depot_cost_reconciliations(adjustment_id, reuse_sale_id) WHERE reuse_sale_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_cost_reconcile_source_seq ON depot_cost_reconciliations(batch_source_id, sequence_no DESC);
CREATE INDEX IF NOT EXISTS ix_cost_reconcile_sale_seq ON depot_cost_reconciliations(reuse_sale_id, sequence_no DESC);
ALTER TABLE depot_reuse_sales ADD COLUMN IF NOT EXISTS unit_cost NUMERIC(30,9);
-- Dữ liệu cũ có vốn gốc bất biến: lưu snapshot; không tự xác định vốn cho dòng còn thiếu.
UPDATE depot_reuse_sales x SET unit_cost = l.allocated_cost / l.quantity
FROM depot_stock_lines l WHERE l.id = x.line_id AND x.unit_cost IS NULL AND l.allocated_cost IS NOT NULL;
-- Mốc nhận hàng bất biến cho FIFO; đơn cũ thiếu event dùng updated_at làm mốc ước tính.
ALTER TABLE pickup_requests ADD COLUMN IF NOT EXISTS stock_received_at TIMESTAMPTZ;
UPDATE pickup_requests p SET stock_received_at = COALESCE(
    (SELECT MIN(e.created_at) FROM employee_collection_events e WHERE e.pickup_request_id = p.id AND e.kind = 'HANDED_OVER'), p.updated_at)
WHERE p.status = 'DONE' AND p.stock_received_at IS NULL;
-- Thống nhất kg đến 0,001 từ nguồn thu gom/phiếu nhập tới lô và cân Factory.
ALTER TABLE pickup_request_items ALTER COLUMN weight_kg TYPE NUMERIC(18,3);
ALTER TABLE inventory_batches ALTER COLUMN declared_weight_kg TYPE NUMERIC(18,3);
ALTER TABLE inventory_batches ALTER COLUMN actual_weight_kg TYPE NUMERIC(18,3);
ALTER TABLE batch_quality_checks ALTER COLUMN actual_weight_kg TYPE NUMERIC(18,3);
ALTER TABLE batch_quality_checks ALTER COLUMN gross_weight_kg TYPE NUMERIC(18,3);
ALTER TABLE batch_quality_checks ALTER COLUMN tare_weight_kg TYPE NUMERIC(18,3);
ALTER TABLE factory_demands ALTER COLUMN required_weight_kg TYPE NUMERIC(18,3);

ALTER TABLE depots ADD COLUMN IF NOT EXISTS verification_status VARCHAR(20) NOT NULL DEFAULT 'UNVERIFIED';
CREATE TABLE IF NOT EXISTS depot_verification_submissions (
    id UUID PRIMARY KEY, depot_id UUID NOT NULL REFERENCES depots(id) ON DELETE CASCADE,
    status VARCHAR(20) NOT NULL CHECK (status IN ('SUBMITTED','APPROVED','REJECTED')),
    note VARCHAR(2000) NOT NULL DEFAULT '', depot_name VARCHAR(255) NOT NULL, depot_address TEXT NOT NULL, tax_code TEXT,
    submitted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reviewed_at TIMESTAMPTZ, reviewed_by UUID REFERENCES users(id), review_reason VARCHAR(2000)
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_depot_verification_pending ON depot_verification_submissions(depot_id) WHERE status = 'SUBMITTED';
CREATE INDEX IF NOT EXISTS ix_depot_verification_queue ON depot_verification_submissions(submitted_at DESC, id);
CREATE TABLE IF NOT EXISTS depot_verification_documents (
    id UUID PRIMARY KEY, submission_id UUID NOT NULL REFERENCES depot_verification_submissions(id) ON DELETE CASCADE,
    name VARCHAR(150) NOT NULL, content_type VARCHAR(50) NOT NULL,
    content BYTEA NOT NULL CHECK (octet_length(content) BETWEEN 1 AND 2097152)
);
-- RETRACK_DEPOT_OPERATIONS_V1
CREATE TABLE IF NOT EXISTS depot_incident_followups (
    id UUID PRIMARY KEY, event_id UUID NOT NULL REFERENCES driver_delivery_events(id) ON DELETE CASCADE,
    status VARCHAR(20) NOT NULL CHECK (status IN ('NOTED','CONTACTING','CLOSED')),
    note VARCHAR(1000) NOT NULL CHECK (length(btrim(note)) > 0), created_by UUID NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_depot_incident_followups_event_date ON depot_incident_followups(event_id, created_at DESC, id);
-- RETRACK_LIQUIDATION_V1: phiên trả giá trọn lô, lịch sử giá bất biến và thỏa thuận ngoài app.
CREATE TABLE IF NOT EXISTS liquidation_lots (
    id UUID PRIMARY KEY, seller_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    title VARCHAR(255) NOT NULL, description VARCHAR(5000) NOT NULL, scope_details VARCHAR(3000) NOT NULL,
    collection_terms VARCHAR(2000) NOT NULL, public_area VARCHAR(255) NOT NULL,
    address VARCHAR(1000) NOT NULL, contact_phone VARCHAR(20) NOT NULL,
    image_urls TEXT[] NOT NULL DEFAULT '{}',
    survey_start TIMESTAMPTZ NOT NULL, survey_end TIMESTAMPTZ NOT NULL,
    bidding_start TIMESTAMPTZ NOT NULL, original_end TIMESTAMPTZ NOT NULL, bidding_end TIMESTAMPTZ NOT NULL,
    collection_deadline TIMESTAMPTZ NOT NULL,
    starting_price NUMERIC(18,0) NOT NULL CHECK (starting_price > 0),
    bid_increment NUMERIC(18,0) NOT NULL CHECK (bid_increment > 0),
    highest_price NUMERIC(18,0), highest_bid_id UUID, bid_count INTEGER NOT NULL DEFAULT 0 CHECK (bid_count >= 0),
    status VARCHAR(20) NOT NULL CHECK (status IN ('PUBLISHED','AWARDED','NO_SALE','CANCELLED')),
    cancel_reason VARCHAR(1000), request_hash VARCHAR(64) NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (survey_start < survey_end AND survey_end <= bidding_start AND bidding_start < original_end
        AND original_end <= bidding_end AND bidding_end <= original_end + INTERVAL '10 minutes'
        AND original_end + INTERVAL '10 minutes' < collection_deadline)
);
CREATE INDEX IF NOT EXISTS ix_liquidation_closing ON liquidation_lots(status, bidding_end);
CREATE INDEX IF NOT EXISTS ix_liquidation_seller ON liquidation_lots(seller_id, created_at DESC);
CREATE TABLE IF NOT EXISTS liquidation_participations (
    id UUID PRIMARY KEY, lot_id UUID NOT NULL REFERENCES liquidation_lots(id) ON DELETE CASCADE,
    owner_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    depot_id UUID NOT NULL REFERENCES depots(id) ON DELETE RESTRICT,
    registered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), survey_declared_at TIMESTAMPTZ,
    UNIQUE(lot_id, owner_id)
);
CREATE TABLE IF NOT EXISTS liquidation_bids (
    id UUID PRIMARY KEY, lot_id UUID NOT NULL REFERENCES liquidation_lots(id) ON DELETE CASCADE,
    owner_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    depot_id UUID NOT NULL REFERENCES depots(id) ON DELETE RESTRICT,
    amount NUMERIC(18,0) NOT NULL CHECK (amount > 0), created_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_liquidation_bid_history ON liquidation_bids(lot_id, owner_id, created_at DESC, id);
CREATE TABLE IF NOT EXISTS liquidation_agreements (
    id UUID PRIMARY KEY, lot_id UUID NOT NULL UNIQUE REFERENCES liquidation_lots(id) ON DELETE RESTRICT,
    winning_bid_id UUID NOT NULL UNIQUE REFERENCES liquidation_bids(id) ON DELETE RESTRICT,
    depot_id UUID NOT NULL REFERENCES depots(id) ON DELETE RESTRICT,
    buyer_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    amount NUMERIC(18,0) NOT NULL CHECK (amount > 0), payment_reference VARCHAR(255),
    payment_reported_at TIMESTAMPTZ, payment_confirmed_at TIMESTAMPTZ,
    owner_collected_at TIMESTAMPTZ, seller_handed_over_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (payment_confirmed_at IS NULL OR payment_reported_at IS NOT NULL),
    CHECK ((owner_collected_at IS NULL AND seller_handed_over_at IS NULL) OR payment_confirmed_at IS NOT NULL)
);
ALTER TABLE depot_stock_receipts ADD COLUMN IF NOT EXISTS bulk_agreement_id UUID
    REFERENCES liquidation_agreements(id) ON DELETE RESTRICT;
CREATE UNIQUE INDEX IF NOT EXISTS ix_receipt_active_bulk_agreement ON depot_stock_receipts(bulk_agreement_id)
    WHERE bulk_agreement_id IS NOT NULL AND status NOT IN ('VOIDED','REJECTED');


-- RETRACK_SCHEMA_END

-- RETRACK_SEED_BEGIN
-- Dữ liệu mẫu nằm cùng file để thành viên chỉ cần chạy một lần.
DO $retrack_seed$
BEGIN
IF current_setting('retrack.seed_demo', true) = 'on' THEN
-- Dữ liệu demo dùng cục bộ/phát triển, không dùng production.
-- Schema đã được dựng ở phần trên; seed chạy trong cùng transaction khi tùy chọn đang bật.
-- Có thể chạy lại; các UUID fixture đã có sẽ được giữ nguyên.
-- Tài khoản demo được tạo khi email/ID chưa tồn tại; mật khẩu chỉ lưu dạng BCrypt.
-- Không dùng thông tin demo cho môi trường thật.



-- Tài khoản đăng nhập cho Admin, bốn Seller, Depot Owner, hai Employee, hai Driver và ba Factory.
INSERT INTO users (id, email, password_hash, role, full_name, phone, is_active, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000001', 'admin@retrack.vn',    '$2a$11$eNEiPwGZnOqVlHiSydDe4exEtVDa5h2t8vR.VBK/tTAv/W1mHBOwW', 'ADMIN',          'Quản trị viên ReTrack', '0900000001', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000002', 'seller@retrack.vn',   '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Nguyễn Thị Lan',        '0911111111', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000003', 'depot@retrack.vn',    '$2a$11$i0GJ0.h60xsCKWZ8XvYkJ.guY1aYe9ykxcsg22/KvM4PZ7kXr6Sce', 'DEPOT_OWNER',    'Ngô Sỹ Giá',             '0922222222', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000004', 'employee@retrack.vn', '$2a$11$7cergszHVwtBQKDOOIM.POdnPLveC9s7edhE2hY6/rStLyg.889hm', 'DEPOT_EMPLOYEE', 'Lê Minh Tuấn',           '0933333333', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000005', 'driver@retrack.vn',   '$2a$11$d7vd3QQIgwwE8U/3VvpwGuThZU0vhoxsA2jrzvkQ5qkL3KiWgx3UG', 'DRIVER',         'Phạm Văn Hùng',          '0944444444', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000006', 'factory@retrack.vn',  '$2a$11$mIHozvQGf7pIuXnWk4j03OKdxo7q27HVkx3LzqUu3cm2rrhjNCzmG', 'FACTORY',        'Eco Plastics Vietnam',   '0955555551', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000007', 'factory2@retrack.vn', '$2a$11$mIHozvQGf7pIuXnWk4j03OKdxo7q27HVkx3LzqUu3cm2rrhjNCzmG', 'FACTORY',        'Công Ty Kim Loại Phú Lợi','0955555552', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000008', 'factory3@retrack.vn', '$2a$11$mIHozvQGf7pIuXnWk4j03OKdxo7q27HVkx3LzqUu3cm2rrhjNCzmG', 'FACTORY',        'Nhà Máy Giấy Miền Trung','0955555553', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000009', 'seller2@retrack.vn',  '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Trần Thị Hương',        '0911111112', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000a', 'seller3@retrack.vn',  '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Phạm Minh Khoa',        '0911111113', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000b', 'seller4@retrack.vn',  '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Lê Thanh Mai',          '0911111114', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000c', 'employee2@retrack.vn','$2a$11$7cergszHVwtBQKDOOIM.POdnPLveC9s7edhE2hY6/rStLyg.889hm', 'DEPOT_EMPLOYEE', 'Nguyễn Quốc Bảo',       '0933333334', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000d', 'driver2@retrack.vn',  '$2a$11$d7vd3QQIgwwE8U/3VvpwGuThZU0vhoxsA2jrzvkQ5qkL3KiWgx3UG', 'DRIVER',         'Trần Văn Hải',          '0944444445', TRUE, NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Chặn việc âm thầm nối dữ liệu demo vào tài khoản cùng email nhưng sai ID/role.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM (VALUES
            ('00000000-0000-0000-0000-000000000001'::uuid, 'admin@retrack.vn', 'ADMIN'),
            ('00000000-0000-0000-0000-000000000002'::uuid, 'seller@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-000000000003'::uuid, 'depot@retrack.vn', 'DEPOT_OWNER'),
            ('00000000-0000-0000-0000-000000000004'::uuid, 'employee@retrack.vn', 'DEPOT_EMPLOYEE'),
            ('00000000-0000-0000-0000-000000000005'::uuid, 'driver@retrack.vn', 'DRIVER'),
            ('00000000-0000-0000-0000-000000000006'::uuid, 'factory@retrack.vn', 'FACTORY'),
            ('00000000-0000-0000-0000-000000000007'::uuid, 'factory2@retrack.vn', 'FACTORY'),
            ('00000000-0000-0000-0000-000000000008'::uuid, 'factory3@retrack.vn', 'FACTORY'),
            ('00000000-0000-0000-0000-000000000009'::uuid, 'seller2@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-00000000000a'::uuid, 'seller3@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-00000000000b'::uuid, 'seller4@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-00000000000c'::uuid, 'employee2@retrack.vn', 'DEPOT_EMPLOYEE'),
            ('00000000-0000-0000-0000-00000000000d'::uuid, 'driver2@retrack.vn', 'DRIVER')
        ) AS expected(id, email, role)
        LEFT JOIN users u ON u.id = expected.id AND u.email = expected.email AND u.role = expected.role
        WHERE u.id IS NULL
    ) THEN
        RAISE EXCEPTION 'Một tài khoản demo đã tồn tại với ID/email/role khác. Không tiếp tục seed để tránh nối nhầm dữ liệu.';
    END IF;
END
$$;

INSERT INTO depots (id, owner_id, name, address, latitude, longitude, rating, contact_phone, description, created_at)
VALUES ('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000003',
        'Kho vựa phế liệu Ngô Sỹ Giá', 'Khu vực X7P4+XF9, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)',
        15.9874125, 108.2562344, 0, '0922222222', 'Kho mẫu phát triển tại Đà Nẵng.', NOW())
ON CONFLICT DO NOTHING;

INSERT INTO depots (id, owner_id, name, address, latitude, longitude, rating, contact_phone, description, created_at)
VALUES
    ('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000003',
     'Điểm thu gom Hải Châu', 'Khu vực Chợ Hàn, 119 Trần Phú, Hải Châu, Đà Nẵng (địa điểm demo)',
     16.0683600, 108.2244300, 0, '0922222222', 'Điểm demo khu vực trung tâm Đà Nẵng.', NOW()),
    ('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000003',
     'Kho thu gom Cẩm Lệ', 'Khu vực Cẩm Lệ, Đà Nẵng (địa điểm demo)',
     16.0150000, 108.2100000, 0, '0922222222', 'Kho demo phía nam trung tâm Đà Nẵng.', NOW())
ON CONFLICT DO NOTHING;

-- Chỉ ba kho demo được đánh dấu đã duyệt; kho thật cần Admin đối soát hồ sơ.
UPDATE depots SET verification_status = 'APPROVED'
WHERE id IN ('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000012')
  AND owner_id = '00000000-0000-0000-0000-000000000003';

INSERT INTO depot_staffs (id, depot_id, user_id, staff_type, is_active)
VALUES
    ('00000000-0000-0000-0000-000000000030', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000031', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000005', 'DRIVER', TRUE),
    ('00000000-0000-0000-0000-000000000032', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000033', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000005', 'DRIVER', TRUE),
    ('00000000-0000-0000-0000-000000000034', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000004', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000035', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000005', 'DRIVER', TRUE),
    ('00000000-0000-0000-0000-000000000036', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000037', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-00000000000d', 'DRIVER', TRUE)
ON CONFLICT DO NOTHING;

-- Mỗi nhà máy có mã vật liệu chuẩn đúng với MaterialCatalog của API.
INSERT INTO factories (id, owner_id, name, address, latitude, longitude, rating, contact_phone,
                       capacity_kg_per_month, minimum_purity_percent, accepted_materials, created_at)
VALUES
    ('00000000-0000-0000-0000-000000000020', '00000000-0000-0000-0000-000000000006', 'Eco Plastics Vietnam',
     'Khu đô thị FPT, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)', 15.9832468, 108.2520905, 0,
     '0955555551', 20000, 0, 'PET,HDPE', NOW()),
    ('00000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000007', 'Công Ty Kim Loại Phú Lợi',
     'Khu vực Hòa Khánh, Đà Nẵng (địa điểm demo)', 16.0670000, 108.1500000, 0,
     '0955555552', 20000, 0, 'ALUMINUM,IRON,STEEL,COPPER', NOW()),
    ('00000000-0000-0000-0000-000000000022', '00000000-0000-0000-0000-000000000008', 'Nhà Máy Giấy Miền Trung',
     'Khu vực Cẩm Lệ, Đà Nẵng (địa điểm demo)', 16.0150000, 108.2100000, 0,
     '0955555553', 20000, 0, 'PAPER,CARDBOARD', NOW())
ON CONFLICT DO NOTHING;

INSERT INTO factory_demands (id, factory_id, material_type, required_weight_kg, min_price_per_kg,
                             max_price_per_kg, deadline, is_active, note, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000040', '00000000-0000-0000-0000-000000000020', 'PET', 5000, 8000, 12000, NOW() + INTERVAL '30 days', TRUE, 'Nhu cầu demo PET.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000041', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 3000, 20000, 30000, NOW() + INTERVAL '30 days', TRUE, 'Nhu cầu demo nhôm.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000042', '00000000-0000-0000-0000-000000000022', 'CARDBOARD', 4000, 2000, 4000, NOW() + INTERVAL '30 days', TRUE, 'Nhu cầu demo bìa carton.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000043', '00000000-0000-0000-0000-000000000020', 'HDPE', 2500, 9000, 14000, NOW() + INTERVAL '14 days', TRUE, 'Nhu cầu demo nhựa HDPE.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000044', '00000000-0000-0000-0000-000000000021', 'COPPER', 1200, 58000, 70000, NOW() + INTERVAL '21 days', TRUE, 'Nhu cầu demo đồng.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000045', '00000000-0000-0000-0000-000000000022', 'PAPER', 3200, 2500, 3800, NOW() + INTERVAL '10 days', TRUE, 'Nhu cầu demo giấy.', NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Nhiều trạng thái từ yêu cầu mới đến hoàn tất để các role thử toàn bộ danh sách và tồn kho.
INSERT INTO pickup_requests (id, seller_id, target_depot_id, accepted_collector_id, description, address,
    latitude, longitude, preferred_datetime, gross_amount, platform_fee_percentage, platform_fee_amount,
    net_amount, status, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001001', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', NULL,
     '[DEMO] Yêu cầu mới để thử Seller → Employee.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() + INTERVAL '2 hours', 0, 5, 0, 0, 'PENDING', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001002', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu đã nhận để thử hồ sơ Employee.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() + INTERVAL '4 hours', 0, 5, 0, 0, 'SCHEDULED', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001003', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu hoàn tất để tạo tồn kho PET mẫu; không phải giao dịch thật.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() - INTERVAL '1 day', 2000000, 5, 100000, 1900000, 'DONE', NOW() - INTERVAL '2 days', NOW() - INTERVAL '1 day'),
    ('00000000-0000-0000-0000-000000001004', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu chờ chủ kho thanh toán cho Seller.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() - INTERVAL '2 hours', 500000, 5, 25000, 475000, 'AWAITING_PAYMENT', NOW() - INTERVAL '1 day', NOW()),
    ('00000000-0000-0000-0000-000000001005', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', NULL,
     '[DEMO] Yêu cầu Seller mới tại Hải Châu.', 'Chợ Hàn, 119 Trần Phú, Hải Châu, Đà Nẵng', 16.0683600, 108.2244300,
     NOW() + INTERVAL '1 day', 0, 5, 0, 0, 'PENDING', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001006', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000012', NULL,
     '[DEMO] Yêu cầu nhôm mới tại Cẩm Lệ.', 'Khu vực Cẩm Lệ, Đà Nẵng', 16.0150000, 108.2100000,
     NOW() + INTERVAL '3 hours', 0, 5, 0, 0, 'PENDING', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001007', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu đã nhận để kiểm tra lịch Employee.', 'Bảo tàng Điêu khắc Chăm, 2 Tháng 9, Hải Châu, Đà Nẵng', 16.0602000, 108.2231700,
     NOW() + INTERVAL '6 hours', 0, 5, 0, 0, 'SCHEDULED', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001008', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Hoàn tất thu gom bìa carton để tạo tồn kho mẫu.', 'Khu vực Cẩm Lệ, Đà Nẵng', 16.0150000, 108.2100000,
     NOW() - INTERVAL '3 days', 320000, 5, 16000, 304000, 'DONE', NOW() - INTERVAL '4 days', NOW() - INTERVAL '3 days'),
    ('00000000-0000-0000-0000-000000001009', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Hoàn tất thu gom nhôm để tạo tồn kho mẫu.', 'Chợ Cồn, 290 Hùng Vương, Hải Châu, Đà Nẵng', 16.0681700, 108.2145300,
     NOW() - INTERVAL '5 days', 1680000, 5, 84000, 1596000, 'DONE', NOW() - INTERVAL '6 days', NOW() - INTERVAL '5 days'),
    ('00000000-0000-0000-0000-000000001021', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c',
     '[DEMO] Đã cân PET, chờ Seller xác nhận.', 'Khu vực Mỹ An, Ngũ Hành Sơn, Đà Nẵng', 16.0355000, 108.2453000,
     NOW() - INTERVAL '3 hours', 600000, 5, 30000, 570000, 'WEIGHED', NOW() - INTERVAL '1 day', NOW()),
    ('00000000-0000-0000-0000-000000001022', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c',
     '[DEMO] Seller đã duyệt cân HDPE, chờ nhân viên bàn giao cho chủ kho.', 'Khu vực Khuê Mỹ, Ngũ Hành Sơn, Đà Nẵng', 16.0127000, 108.2544000,
     NOW() - INTERVAL '1 day', 420000, 5, 21000, 399000, 'SELLER_CONFIRMED', NOW() - INTERVAL '2 days', NOW()),
    ('00000000-0000-0000-0000-000000001023', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Chủ kho đã gửi tiền, chờ Seller xác nhận.', 'Khu vực Thanh Bình, Hải Châu, Đà Nẵng', 16.0770000, 108.2070000,
     NOW() - INTERVAL '2 days', 360000, 5, 18000, 342000, 'PAYMENT_SENT', NOW() - INTERVAL '3 days', NOW()),
    ('00000000-0000-0000-0000-000000001024', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c',
     '[DEMO] PET đã hoàn tất, bổ sung tồn kho cho lô xuất.', 'Khu vực Hòa Quý, Ngũ Hành Sơn, Đà Nẵng', 15.9830000, 108.2530000,
     NOW() - INTERVAL '6 days', 2000000, 5, 100000, 1900000, 'DONE', NOW() - INTERVAL '7 days', NOW() - INTERVAL '6 days'),
    ('00000000-0000-0000-0000-000000001025', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Nhôm đã hoàn tất, bổ sung tồn kho cho lô xuất.', 'Khu vực Hòa Cường, Hải Châu, Đà Nẵng', 16.0370000, 108.2210000,
     NOW() - INTERVAL '8 days', 4200000, 5, 210000, 3990000, 'DONE', NOW() - INTERVAL '9 days', NOW() - INTERVAL '8 days'),
    ('00000000-0000-0000-0000-000000001026', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Carton đã hoàn tất, bổ sung tồn kho cho lô xuất.', 'Khu vực Hòa Xuân, Cẩm Lệ, Đà Nẵng', 16.0010000, 108.2110000,
     NOW() - INTERVAL '10 days', 480000, 5, 24000, 456000, 'DONE', NOW() - INTERVAL '11 days', NOW() - INTERVAL '10 days'),
    ('00000000-0000-0000-0000-000000001027', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', NULL,
     '[DEMO] Yêu cầu mới để kiểm tra danh sách có phân trang.', 'Khu vực Non Nước, Ngũ Hành Sơn, Đà Nẵng', 15.9990000, 108.2740000,
     NOW() + INTERVAL '2 days', 0, 5, 0, 0, 'PENDING', NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO pickup_request_items (id, pickup_request_id, material_type, weight_kg, price_per_kg, sub_total)
VALUES
    ('00000000-0000-0000-0000-000000001011', '00000000-0000-0000-0000-000000001003', 'PET', 200, 10000, 2000000),
    ('00000000-0000-0000-0000-000000001012', '00000000-0000-0000-0000-000000001004', 'PET', 50, 10000, 500000),
    ('00000000-0000-0000-0000-000000001013', '00000000-0000-0000-0000-000000001008', 'CARDBOARD', 80, 4000, 320000),
    ('00000000-0000-0000-0000-000000001014', '00000000-0000-0000-0000-000000001009', 'ALUMINUM', 60, 28000, 1680000),
    ('00000000-0000-0000-0000-000000001031', '00000000-0000-0000-0000-000000001021', 'PET', 60, 10000, 600000),
    ('00000000-0000-0000-0000-000000001032', '00000000-0000-0000-0000-000000001022', 'HDPE', 35, 12000, 420000),
    ('00000000-0000-0000-0000-000000001033', '00000000-0000-0000-0000-000000001023', 'PAPER', 120, 3000, 360000),
    ('00000000-0000-0000-0000-000000001034', '00000000-0000-0000-0000-000000001024', 'PET', 200, 10000, 2000000),
    ('00000000-0000-0000-0000-000000001035', '00000000-0000-0000-0000-000000001025', 'ALUMINUM', 150, 28000, 4200000),
    ('00000000-0000-0000-0000-000000001036', '00000000-0000-0000-0000-000000001026', 'CARDBOARD', 120, 4000, 480000)
ON CONFLICT DO NOTHING;

-- Hai đơn demo đang chờ Seller/xử lý bàn giao cần đủ snapshot như luồng Employee.
-- Chỉ bổ sung fixture demo còn thiếu, không ghi đè ảnh/lịch sử của đơn đã thao tác thật.
-- Ảnh mẫu được ghi rõ DEMO, không phải bằng chứng camera/GPS thực tế.
INSERT INTO pickup_checkins (pickup_request_id, employee_id, latitude, longitude,
    accuracy_meters, distance_meters, location_recorded_at, photo_taken_at, checked_in_at, image_url, revision)
SELECT p.id, p.accepted_collector_id, p.latitude, p.longitude, 5, 0,
    p.updated_at - INTERVAL '10 minutes', p.updated_at - INTERVAL '10 minutes',
    p.updated_at - INTERVAL '10 minutes', '/demo/collection-checkin.svg', 1
FROM pickup_requests p
WHERE p.id IN ('00000000-0000-0000-0000-000000001021', '00000000-0000-0000-0000-000000001022')
    AND p.description LIKE '[DEMO]%' AND p.status IN ('WEIGHED', 'SELLER_CONFIRMED')
    AND p.checkin_image_url IS NULL
    AND NOT EXISTS (SELECT 1 FROM employee_collection_events e WHERE e.pickup_request_id = p.id)
ON CONFLICT (pickup_request_id) DO NOTHING;

UPDATE pickup_requests p SET checkin_image_url = c.image_url
FROM pickup_checkins c
WHERE p.id = c.pickup_request_id
    AND p.id IN ('00000000-0000-0000-0000-000000001021', '00000000-0000-0000-0000-000000001022')
    AND p.description LIKE '[DEMO]%' AND p.status IN ('WEIGHED', 'SELLER_CONFIRMED')
    AND p.checkin_image_url IS NULL AND c.image_url = '/demo/collection-checkin.svg';

INSERT INTO employee_collection_events (id, pickup_request_id, employee_id, kind, revision,
    from_status, to_status, snapshot_json, created_at)
SELECT gen_random_uuid(), p.id, c.employee_id, 'SUBMITTED', c.revision, 'IN_PROGRESS', 'WEIGHED',
    jsonb_build_object('Items', rows.items, 'GrossAmount', p.gross_amount,
        'PlatformFeePercentage', p.platform_fee_percentage, 'PlatformFeeAmount', p.platform_fee_amount,
        'NetAmount', p.net_amount)::TEXT, p.updated_at
FROM pickup_requests p
JOIN pickup_checkins c ON c.pickup_request_id = p.id AND c.employee_id = p.accepted_collector_id
JOIN LATERAL (
    SELECT jsonb_agg(jsonb_build_object('Id', i.id, 'MaterialType', i.material_type,
        'WeightKg', i.weight_kg, 'PricePerKg', i.price_per_kg, 'SubTotal', i.sub_total)
        ORDER BY i.material_type, i.id) AS items, SUM(i.sub_total) AS gross
    FROM pickup_request_items i WHERE i.pickup_request_id = p.id
) rows ON rows.items IS NOT NULL AND rows.gross = p.gross_amount
WHERE p.id IN ('00000000-0000-0000-0000-000000001021', '00000000-0000-0000-0000-000000001022')
    AND p.description LIKE '[DEMO]%' AND p.status IN ('WEIGHED', 'SELLER_CONFIRMED')
    AND c.image_url = '/demo/collection-checkin.svg' AND p.checkin_image_url = c.image_url
ON CONFLICT (pickup_request_id, kind, revision) DO NOTHING;

-- Một lời mời trực tiếp chờ nhà máy quyết định lô đầu tiên; không seed APPROVED.
INSERT INTO factory_depot_partnerships (id, depot_id, factory_id, status, blocked_by_factory, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000050', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PENDING', FALSE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000051', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'APPROVED', FALSE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000052', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000022', 'PENDING', FALSE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000053', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000021', 'BLOCKED', TRUE, NOW(), NOW())
ON CONFLICT (depot_id, factory_id) DO NOTHING;

INSERT INTO inventory_batches (id, code, depot_id, direct_offer_factory_id, material_type, declared_weight_kg,
                               description, status, image_urls, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001101', 'LO-DEMO-FACTORY-01',
        '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PET', 100,
        '[DEMO] Lô đầu tiên chờ Eco Plastics nhận; sau QC nhà máy mới quyết định hợp tác lâu dài.',
        'PENDING_APPROVAL', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Hai lô công khai ở hai kho khác nhau, vật liệu khớp danh mục nhận của từng Factory.
INSERT INTO inventory_batches (id, code, depot_id, material_type, declared_weight_kg, description, status, image_urls, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001102', 'LO-DEMO-CARD-01', '00000000-0000-0000-0000-000000000012', 'CARDBOARD', 50,
     '[DEMO] Bìa carton sạch, lô công khai để thử lọc vật liệu và nhu cầu Factory.', 'MARKETPLACE', ARRAY[]::TEXT[], NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001103', 'LO-DEMO-AL-01', '00000000-0000-0000-0000-000000000011', 'ALUMINUM', 40,
     '[DEMO] Nhôm phân loại, lô công khai để thử Factory nhận mua.', 'MARKETPLACE', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO inventory_batches (id, code, depot_id, target_factory_id, material_type, declared_weight_kg, description, status, image_urls, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001104', 'LO-DEMO-AL-02',
        '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 20,
        '[DEMO] Lô demo đã được Factory nhận theo quan hệ hợp tác có sẵn, sẵn sàng giao tài xế.',
        'TRANSPORT_READY', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO inventory_batches (id, code, depot_id, direct_offer_factory_id, material_type, declared_weight_kg, description, status, image_urls, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001105', 'LO-DEMO-CARD-02',
        '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000022', 'CARDBOARD', 30,
        '[DEMO] Lô carton đầu tiên được chỉ định Nhà Máy Giấy Miền Trung, chờ nhà máy nhận.',
        'PENDING_APPROVAL', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO transport_jobs (id, batch_id, status, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001201', '00000000-0000-0000-0000-000000001104', 'PENDING', NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Lịch sử đã giao, đã nhận, cân/KCS và quyết toán để Depot/Factory xem báo cáo.
INSERT INTO inventory_batches (id, code, depot_id, target_factory_id, material_type, declared_weight_kg,
    description, status, actual_weight_kg, factory_received_at, factory_decided_at,
    agreed_price_per_kg, gross_amount, platform_fee_amount, net_amount, payment_reference, settled_at,
    image_urls, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001111', 'LO-DEMO-PET-QC', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PET', 80,
     '[DEMO] Đã nhận hàng, đang chờ cân tại nhà máy.', 'RECEIVED', NULL, NOW() - INTERVAL '2 days', NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days'),
    ('00000000-0000-0000-0000-000000001112', 'LO-DEMO-AL-KCS', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 40,
     '[DEMO] Đã cân, chờ nhà máy kết luận KCS.', 'WEIGHED', 39, NOW() - INTERVAL '4 days', NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001113', 'LO-DEMO-CARD-VER', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000022', 'CARDBOARD', 35,
     '[DEMO] Đã đạt KCS, đang chờ nhà máy quyết toán.', 'VERIFIED', 34, NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days',
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '6 days', NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001114', 'LO-DEMO-PET-PAID', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PET', 70,
     '[DEMO] Đã quyết toán với Factory, dùng cho báo cáo doanh thu.', 'COMPLETED', 69, NOW() - INTERVAL '9 days', NOW() - INTERVAL '8 days',
     10000, 690000, 34500, 655500, 'DEMO-FACTORY-PET-01', NOW() - INTERVAL '7 days', ARRAY[]::TEXT[], NOW() - INTERVAL '10 days', NOW() - INTERVAL '7 days'),
    ('00000000-0000-0000-0000-000000001115', 'LO-DEMO-AL-REJ', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 30,
     '[DEMO] Lô bị từ chối KCS; hàng chưa được ghi nhận trả về kho.', 'REJECTED', 29, NOW() - INTERVAL '7 days', NOW() - INTERVAL '6 days',
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '8 days', NOW() - INTERVAL '6 days')
ON CONFLICT DO NOTHING;

INSERT INTO transport_jobs (id, batch_id, driver_id, status, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001211', '00000000-0000-0000-0000-000000001111', '00000000-0000-0000-0000-000000000005', 'DELIVERED', NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days'),
    ('00000000-0000-0000-0000-000000001212', '00000000-0000-0000-0000-000000001112', '00000000-0000-0000-0000-00000000000d', 'DELIVERED', NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001213', '00000000-0000-0000-0000-000000001113', '00000000-0000-0000-0000-000000000005', 'DELIVERED', NOW() - INTERVAL '6 days', NOW() - INTERVAL '5 days'),
    ('00000000-0000-0000-0000-000000001214', '00000000-0000-0000-0000-000000001114', '00000000-0000-0000-0000-000000000005', 'DELIVERED', NOW() - INTERVAL '10 days', NOW() - INTERVAL '9 days'),
    ('00000000-0000-0000-0000-000000001215', '00000000-0000-0000-0000-000000001115', '00000000-0000-0000-0000-00000000000d', 'DELIVERED', NOW() - INTERVAL '8 days', NOW() - INTERVAL '7 days')
ON CONFLICT DO NOTHING;

INSERT INTO batch_quality_checks (id, batch_id, factory_id, actual_weight_kg, grade, agreed_price_per_kg,
    gross_amount, platform_fee_percentage, platform_fee_amount, net_amount, is_accepted,
    gross_weight_kg, tare_weight_kg, difference_percentage, ticket_number, purity_percent,
    moisture_percent, contamination_percent, quality_note, resolution, invoice_number, invoice_status, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001221', '00000000-0000-0000-0000-000000001112', '00000000-0000-0000-0000-000000000021', 39, 'PENDING', 0,
     0, 0, 0, 0, FALSE, 45, 6, -2.50, 'DEMO-CAN-AL-01', NULL, NULL, NULL,
     '[DEMO] Đã cân, chờ kết luận KCS.', NULL, NULL, NULL, NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001222', '00000000-0000-0000-0000-000000001113', '00000000-0000-0000-0000-000000000022', 34, 'A', 0,
     0, 0, 0, 0, TRUE, 39, 5, -2.86, 'DEMO-CAN-CARD-01', 95, 3, 2,
     '[DEMO] Bìa carton đạt yêu cầu.', NULL, NULL, NULL, NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001223', '00000000-0000-0000-0000-000000001114', '00000000-0000-0000-0000-000000000020', 69, 'A', 10000,
     690000, 5, 34500, 655500, TRUE, 75, 6, -1.43, 'DEMO-CAN-PET-01', 97, 2, 1,
     '[DEMO] PET đã đạt KCS và quyết toán; nhà máy có thể tải hóa đơn qua giao diện.', NULL, NULL, NULL, NOW() - INTERVAL '8 days'),
    ('00000000-0000-0000-0000-000000001224', '00000000-0000-0000-0000-000000001115', '00000000-0000-0000-0000-000000000021', 29, 'C', 0,
     0, 0, 0, 0, FALSE, 35, 6, -3.33, 'DEMO-CAN-AL-02', 72, 10, 18,
     '[DEMO] Lẫn tạp chất; chờ hai bên thống nhất hướng trả hàng.', 'RETURN', NULL, NULL, NOW() - INTERVAL '6 days')
ON CONFLICT DO NOTHING;

INSERT INTO notifications (id, user_id, title, message, is_read, created_at, transport_job_id)
VALUES ('00000000-0000-0000-0000-000000001301', '00000000-0000-0000-0000-000000000005',
        'Chuyến xe demo sẵn sàng', 'Lô LO-DEMO-AL-02 tại điểm thu gom Hải Châu đang chờ tài xế nhận.', FALSE, NOW(),
        '00000000-0000-0000-0000-000000001201')
ON CONFLICT DO NOTHING;

-- Giá tham khảo phát triển cho Admin/Depot/Factory; nguồn được ghi rõ là mẫu.
INSERT INTO market_prices (id, material_type, price_per_kg, effective_date, source, created_at)
VALUES
    ('00000000-0000-0000-0000-000000000060', 'PET', 10000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000061', 'HDPE', 12000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000062', 'PAPER', 3000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000063', 'CARDBOARD', 2500, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000064', 'ALUMINUM', 28000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000065', 'IRON', 12000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000066', 'STEEL', 15000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000067', 'COPPER', 65000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW())
ON CONFLICT DO NOTHING;

-- Seller/Factory đánh giá sau các giao dịch đã hoàn tất; dữ liệu này hiện lên hồ sơ đối tác.
INSERT INTO seller_depot_reviews (id, pickup_request_id, depot_id, rating, comment, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001401', '00000000-0000-0000-0000-000000001003', '00000000-0000-0000-0000-000000000010', 5, '[DEMO] Nhân viên thu gom đúng giờ.', NOW() - INTERVAL '1 day'),
    ('00000000-0000-0000-0000-000000001402', '00000000-0000-0000-0000-000000001025', '00000000-0000-0000-0000-000000000011', 4, '[DEMO] Cân và thanh toán rõ ràng.', NOW() - INTERVAL '7 days'),
    ('00000000-0000-0000-0000-000000001403', '00000000-0000-0000-0000-000000001026', '00000000-0000-0000-0000-000000000012', 5, '[DEMO] Thu gom nhanh.', NOW() - INTERVAL '9 days')
ON CONFLICT DO NOTHING;

INSERT INTO factory_depot_reviews (id, batch_id, factory_id, depot_id, rating, comment, created_at)
VALUES ('00000000-0000-0000-0000-000000001411', '00000000-0000-0000-0000-000000001114',
        '00000000-0000-0000-0000-000000000020', '00000000-0000-0000-0000-000000000010', 5,
        '[DEMO] PET được phân loại sạch, đúng mô tả.', NOW() - INTERVAL '6 days')
ON CONFLICT DO NOTHING;

-- Ghi phí đúng một lần cho mỗi nguồn đã thanh toán, phục vụ báo cáo Admin/Depot.
INSERT INTO platform_transactions (id, source_type, source_id, fee_amount, payer_id, transaction_amount,
                                   fee_percentage, description, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001501', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001003', 100000,
     '00000000-0000-0000-0000-000000000003', 2000000, 5, '[DEMO] Phí thu gom PET đã hoàn tất.', NOW() - INTERVAL '1 day'),
    ('00000000-0000-0000-0000-000000001502', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001025', 210000,
     '00000000-0000-0000-0000-000000000003', 4200000, 5, '[DEMO] Phí thu gom nhôm đã hoàn tất.', NOW() - INTERVAL '8 days'),
    ('00000000-0000-0000-0000-000000001503', 'BATCH_ORDER', '00000000-0000-0000-0000-000000001114', 34500,
     '00000000-0000-0000-0000-000000000006', 690000, 5, '[DEMO] Phí quyết toán lô PET.', NOW() - INTERVAL '7 days'),
    ('00000000-0000-0000-0000-000000001504', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001008', 16000,
     '00000000-0000-0000-0000-000000000003', 320000, 5, '[DEMO] Phí thu gom carton đã hoàn tất.', NOW() - INTERVAL '3 days'),
    ('00000000-0000-0000-0000-000000001505', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001009', 84000,
     '00000000-0000-0000-0000-000000000003', 1680000, 5, '[DEMO] Phí thu gom nhôm đã hoàn tất.', NOW() - INTERVAL '5 days'),
    ('00000000-0000-0000-0000-000000001506', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001024', 100000,
     '00000000-0000-0000-0000-000000000003', 2000000, 5, '[DEMO] Phí thu gom PET đã hoàn tất.', NOW() - INTERVAL '6 days'),
    ('00000000-0000-0000-0000-000000001507', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001026', 24000,
     '00000000-0000-0000-0000-000000000003', 480000, 5, '[DEMO] Phí thu gom carton đã hoàn tất.', NOW() - INTERVAL '10 days')
ON CONFLICT DO NOTHING;

-- Hóa đơn kỳ hiện tại khớp tổng phí đã ghi nhận ở các giao dịch demo phía trên.
INSERT INTO platform_invoices (id, payer_id, period_year, period_month, total_fee_amount, status, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001511', '00000000-0000-0000-0000-000000000003',
     EXTRACT(YEAR FROM CURRENT_DATE)::int,
     EXTRACT(MONTH FROM CURRENT_DATE)::int,
     COALESCE((SELECT SUM(fee_amount) FROM platform_transactions
               WHERE payer_id = '00000000-0000-0000-0000-000000000003'
                 AND created_at >= date_trunc('month', NOW())
                 AND created_at < date_trunc('month', NOW()) + INTERVAL '1 month'), 0), 'PENDING', NOW()),
    ('00000000-0000-0000-0000-000000001512', '00000000-0000-0000-0000-000000000006',
     EXTRACT(YEAR FROM CURRENT_DATE)::int,
     EXTRACT(MONTH FROM CURRENT_DATE)::int,
     COALESCE((SELECT SUM(fee_amount) FROM platform_transactions
               WHERE payer_id = '00000000-0000-0000-0000-000000000006'
                 AND created_at >= date_trunc('month', NOW())
                 AND created_at < date_trunc('month', NOW()) + INTERVAL '1 month'), 0), 'PENDING', NOW())
ON CONFLICT DO NOTHING;

INSERT INTO notifications (id, user_id, title, message, is_read, created_at, pickup_request_id)
VALUES
    ('00000000-0000-0000-0000-000000001311', '00000000-0000-0000-0000-000000000003', 'Đơn chờ thanh toán', '[DEMO] Đơn 1004 đang chờ chủ kho chuyển tiền.', FALSE, NOW(), '00000000-0000-0000-0000-000000001004'),
    ('00000000-0000-0000-0000-000000001312', '00000000-0000-0000-0000-000000000009', 'Chờ xác nhận đã nhận tiền', '[DEMO] Chủ kho đã gửi tiền cho đơn 1023.', FALSE, NOW(), '00000000-0000-0000-0000-000000001023'),
    ('00000000-0000-0000-0000-000000001313', '00000000-0000-0000-0000-00000000000c', 'Đơn chờ thu gom', '[DEMO] Seller mới đã gửi yêu cầu tại Ngũ Hành Sơn.', FALSE, NOW(), '00000000-0000-0000-0000-000000001027')
ON CONFLICT DO NOTHING;

INSERT INTO audit_logs (id, user_id, action, entity_name, entity_id, new_data, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001521', '00000000-0000-0000-0000-000000000001', 'CREATE', 'MarketPrice',
     '00000000-0000-0000-0000-000000000060', '{"materialType":"PET","demo":true}', NOW() - INTERVAL '2 days'),
    ('00000000-0000-0000-0000-000000001522', '00000000-0000-0000-0000-000000000001', 'GENERATE', 'PlatformInvoice',
     '00000000-0000-0000-0000-000000001511', '{"demo":true}', NOW() - INTERVAL '5 days')
ON CONFLICT DO NOTHING;

-- Nhập kho đa nguồn demo: chỉ thêm khi tồn tại kho/chủ kho demo tương ứng.
-- Không đổi trạng thái hay chi phí của phiếu đã được người dùng thao tác.
INSERT INTO depot_stock_receipts
    (id, depot_id, created_by, source_type, reference, status, received_at, purchase_cost,
     costs_confirmed, request_hash, created_at, reviewed_at, reviewed_by)
SELECT '00000000-0000-0000-0000-000000001701', d.id, d.owner_id,
    'EXTERNAL_PURCHASE', '[DEMO] Lô sắt và ghế thanh lý tại Ngũ Hành Sơn', 'POSTED',
    NOW() - INTERVAL '2 days', 10000000, TRUE, repeat('0', 64),
    NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', d.owner_id
FROM depots d WHERE d.id = '00000000-0000-0000-0000-000000000010'
ON CONFLICT DO NOTHING;

INSERT INTO depot_stock_lines
    (id, receipt_id, kind, material_type, name, unit, quantity, reference_price, direct_cost, allocated_cost, condition_note, position)
SELECT v.id::uuid, '00000000-0000-0000-0000-000000001701', v.kind, v.material_type, v.name,
    v.unit, v.quantity, v.reference_price, 0, v.allocated_cost, v.condition_note, v.position
FROM (VALUES
    ('00000000-0000-0000-0000-000000001711', 'MATERIAL', 'IRON', 'Sắt', 'kg', 1000, 6000, 6109091, 'Hàng demo đã phân loại', 0),
    ('00000000-0000-0000-0000-000000001712', 'REUSABLE', NULL, 'Ghế thanh lý', 'cái', 10, 500000, 5090909, 'Dữ liệu demo, cần xem tình trạng thực tế trước bán', 1)
) AS v(id, kind, material_type, name, unit, quantity, reference_price, allocated_cost, condition_note, position)
WHERE EXISTS (SELECT 1 FROM depot_stock_receipts WHERE id = '00000000-0000-0000-0000-000000001701')
ON CONFLICT DO NOTHING;

INSERT INTO depot_stock_expenses (id, receipt_id, category, description, amount)
SELECT v.id::uuid, '00000000-0000-0000-0000-000000001701', v.category, v.description, v.amount
FROM (VALUES
    ('00000000-0000-0000-0000-000000001721', 'LABOR', '[DEMO] Công thu gom và bốc xếp', 1000000),
    ('00000000-0000-0000-0000-000000001722', 'FUEL', '[DEMO] Xăng xe đầu vào phân bổ cho lô', 200000)
) AS v(id, category, description, amount)
WHERE EXISTS (SELECT 1 FROM depot_stock_receipts WHERE id = '00000000-0000-0000-0000-000000001701')
ON CONFLICT DO NOTHING;
-- Đầu ra contract Seller mẫu cho Depot; không thay tính năng đăng lô của TV1.
INSERT INTO liquidation_lots
    (id, seller_id, title, description, scope_details, collection_terms, public_area, address, contact_phone,
     survey_start, survey_end, bidding_start, original_end, bidding_end, collection_deadline, starting_price, bid_increment, status, request_hash)
SELECT v.id::uuid, '00000000-0000-0000-0000-000000000002', v.title,
    '[DEMO] Tài sản thanh lý do bên bán mô tả; cần tự khảo sát hiện trạng.',
    'Khoảng 1.000 kg sắt và 10 ghế còn dùng được; cân/phân loại thực tế sau khi nhận.',
    'Trả giá trọn lô; hai bên tự chuyển tiền và thu gom ngoài hệ thống.',
    'Ngũ Hành Sơn, Đà Nẵng', 'Khu vực đường Châu Thị Vĩnh Tế, Đà Nẵng (địa chỉ demo)', '0911111111',
    NOW() - INTERVAL '1 day', v.survey_end, v.bidding_start, v.bidding_end, v.bidding_end,
    NOW() + INTERVAL '7 days', 10000000, 100000, 'PUBLISHED', 'DEMO-CONTRACT-V1'
FROM (VALUES
    ('00000000-0000-0000-0000-000000001801', '[DEMO] Lô khách sạn đang khảo sát', NOW() + INTERVAL '1 day', NOW() + INTERVAL '2 days', NOW() + INTERVAL '3 days'),
    ('00000000-0000-0000-0000-000000001802', '[DEMO] Lô thiết bị đang mở trả giá', NOW() - INTERVAL '2 hours', NOW() - INTERVAL '1 hour', NOW() + INTERVAL '1 day')
) AS v(id, title, survey_end, bidding_start, bidding_end)
WHERE EXISTS (SELECT 1 FROM users WHERE id = '00000000-0000-0000-0000-000000000002' AND role = 'SELLER')
ON CONFLICT DO NOTHING;

INSERT INTO liquidation_participations (id, lot_id, owner_id, depot_id, registered_at, survey_declared_at)
SELECT '00000000-0000-0000-0000-000000001811', '00000000-0000-0000-0000-000000001802',
    '00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000010', NOW() - INTERVAL '3 hours', NOW() - INTERVAL '2 hours'
WHERE EXISTS (SELECT 1 FROM liquidation_lots WHERE id = '00000000-0000-0000-0000-000000001802')
    AND EXISTS (SELECT 1 FROM depots WHERE id = '00000000-0000-0000-0000-000000000010' AND owner_id = '00000000-0000-0000-0000-000000000003')
ON CONFLICT DO NOTHING;
END IF;
END;
$retrack_seed$;
-- RETRACK_SEED_END

COMMIT;
