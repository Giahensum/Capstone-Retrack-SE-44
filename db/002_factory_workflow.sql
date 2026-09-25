-- Factory workflow extension. Safe to run after init_postgres.sql.
ALTER TABLE factories
    ADD COLUMN IF NOT EXISTS tax_code VARCHAR(50),
    ADD COLUMN IF NOT EXISTS industrial_zone VARCHAR(200),
    ADD COLUMN IF NOT EXISTS contact_phone VARCHAR(30),
    ADD COLUMN IF NOT EXISTS business_license_url TEXT,
    ADD COLUMN IF NOT EXISTS environmental_license_url TEXT,
    ADD COLUMN IF NOT EXISTS capacity_kg_per_month DECIMAL(18, 2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS minimum_purity_percent DECIMAL(5, 2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS accepted_materials TEXT NOT NULL DEFAULT 'PET';

ALTER TABLE factory_demands
    ADD COLUMN IF NOT EXISTS note TEXT,
    ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

ALTER TABLE inventory_batches
    ADD COLUMN IF NOT EXISTS actual_weight_kg DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS factory_received_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS factory_decided_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS rejection_reason TEXT,
    ADD COLUMN IF NOT EXISTS agreed_price_per_kg DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS gross_amount DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS platform_fee_amount DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS net_amount DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS payment_reference VARCHAR(200),
    ADD COLUMN IF NOT EXISTS settled_at TIMESTAMPTZ;

ALTER TABLE inventory_batches
    ADD COLUMN IF NOT EXISTS direct_offer_factory_id UUID REFERENCES factories(id);

ALTER TABLE batch_quality_checks
    ADD COLUMN IF NOT EXISTS gross_weight_kg DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS tare_weight_kg DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS difference_percentage DECIMAL(8, 2),
    ADD COLUMN IF NOT EXISTS ticket_number VARCHAR(100),
    ADD COLUMN IF NOT EXISTS ticket_image_url TEXT,
    ADD COLUMN IF NOT EXISTS purity_percent DECIMAL(5, 2),
    ADD COLUMN IF NOT EXISTS moisture_percent DECIMAL(5, 2),
    ADD COLUMN IF NOT EXISTS contamination_percent DECIMAL(5, 2),
    ADD COLUMN IF NOT EXISTS quality_note TEXT,
    ADD COLUMN IF NOT EXISTS resolution VARCHAR(20),
    ADD COLUMN IF NOT EXISTS invoice_number VARCHAR(100),
    ADD COLUMN IF NOT EXISTS invoice_file_url TEXT,
    ADD COLUMN IF NOT EXISTS invoice_status VARCHAR(30);

CREATE TABLE IF NOT EXISTS market_prices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    material_type VARCHAR(100) NOT NULL,
    price_per_kg DECIMAL(18, 2) NOT NULL,
    effective_date TIMESTAMPTZ NOT NULL,
    source TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
