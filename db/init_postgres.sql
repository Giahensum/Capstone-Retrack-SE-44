-- ============================================================
-- ReNATS_DB - PostgreSQL Init Script
-- Converted from MSSQL (renat_db.docx)
-- Run: psql -U postgres -f init_postgres.sql
-- ============================================================

-- Tạo database (chạy riêng nếu cần)
-- CREATE DATABASE "ReNATS_DB";
-- \c "ReNATS_DB";

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
VALUES ('PLATFORM_FEE_PERCENTAGE', '1.00', 'Phí nền tảng 1%')
ON CONFLICT (config_key) DO NOTHING;

-- Bảng người dùng
CREATE TABLE IF NOT EXISTS users (
    id              UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
    email           VARCHAR(255) UNIQUE NOT NULL,
    password_hash   TEXT         NOT NULL,
    role            VARCHAR(50)  NOT NULL,   -- SELLER, DEPOT_OWNER, DEPOT_EMPLOYEE, DRIVER, FACTORY, ADMIN
    full_name       VARCHAR(255) NOT NULL,
    phone           VARCHAR(20)  NOT NULL,
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
    created_at      TIMESTAMPTZ  DEFAULT NOW()
);

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
-- MODULE 2: GIAI ĐOẠN 1 - THU GOM PHẾ LIỆU & PHÍ 1%
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
    -- Hạch toán tài chính (có phí 1%)
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
    CONSTRAINT uq_factory_depot UNIQUE (depot_id, factory_id)
);

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
    -- MARKETPLACE, PENDING_APPROVAL, TRANSPORT_READY, COMPLETED
    created_at          TIMESTAMPTZ  DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  DEFAULT NOW()
);

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
    -- Hạch toán tài chính (có phí 1%)
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
    description TEXT,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

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
CREATE TRIGGER trg_system_configs_upd
    BEFORE UPDATE ON system_configs
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho users
CREATE TRIGGER trg_users_upd
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho pickup_requests
CREATE TRIGGER trg_pickup_requests_upd
    BEFORE UPDATE ON pickup_requests
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho inventory_batches
CREATE TRIGGER trg_inventory_batches_upd
    BEFORE UPDATE ON inventory_batches
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho transport_jobs
CREATE TRIGGER trg_transport_jobs_upd
    BEFORE UPDATE ON transport_jobs
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Trigger cho factory_depot_partnerships
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
CREATE INDEX IF NOT EXISTS idx_pt_source       ON platform_transactions(source_type, source_id);
