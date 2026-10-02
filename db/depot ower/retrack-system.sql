CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE platform_transactions (
    id uuid NOT NULL,
    source_type character varying(50) NOT NULL,
    source_id uuid NOT NULL,
    fee_amount numeric NOT NULL,
    description text,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_platform_transactions" PRIMARY KEY (id)
);

CREATE TABLE system_configs (
    config_key character varying(50) NOT NULL,
    config_value text NOT NULL,
    description text,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_system_configs" PRIMARY KEY (config_key)
);

CREATE TABLE users (
    id uuid NOT NULL,
    email character varying(255) NOT NULL,
    password_hash text NOT NULL,
    role character varying(50) NOT NULL,
    full_name character varying(255) NOT NULL,
    phone character varying(20) NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_users" PRIMARY KEY (id)
);

CREATE TABLE depots (
    id uuid NOT NULL,
    owner_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    address text NOT NULL,
    latitude numeric,
    longitude numeric,
    rating numeric NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_depots" PRIMARY KEY (id),
    CONSTRAINT "FK_depots_users_owner_id" FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE factories (
    id uuid NOT NULL,
    owner_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    address text NOT NULL,
    latitude numeric,
    longitude numeric,
    rating numeric NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_factories" PRIMARY KEY (id),
    CONSTRAINT "FK_factories_users_owner_id" FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE depot_staffs (
    id uuid NOT NULL,
    depot_id uuid NOT NULL,
    user_id uuid NOT NULL,
    staff_type character varying(50) NOT NULL,
    is_active boolean NOT NULL,
    CONSTRAINT "PK_depot_staffs" PRIMARY KEY (id),
    CONSTRAINT "FK_depot_staffs_depots_depot_id" FOREIGN KEY (depot_id) REFERENCES depots (id) ON DELETE CASCADE,
    CONSTRAINT "FK_depot_staffs_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE pickup_requests (
    id uuid NOT NULL,
    seller_id uuid NOT NULL,
    target_depot_id uuid,
    accepted_collector_id uuid,
    description text,
    request_image_url text,
    address text NOT NULL,
    latitude numeric,
    longitude numeric,
    preferred_datetime timestamp with time zone,
    checkin_image_url text,
    gross_amount numeric NOT NULL,
    platform_fee_percentage numeric NOT NULL,
    platform_fee_amount numeric NOT NULL,
    net_amount numeric NOT NULL,
    payment_proof_url text,
    status character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_pickup_requests" PRIMARY KEY (id),
    CONSTRAINT "FK_pickup_requests_depots_target_depot_id" FOREIGN KEY (target_depot_id) REFERENCES depots (id),
    CONSTRAINT "FK_pickup_requests_users_accepted_collector_id" FOREIGN KEY (accepted_collector_id) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT "FK_pickup_requests_users_seller_id" FOREIGN KEY (seller_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE factory_demands (
    id uuid NOT NULL,
    factory_id uuid NOT NULL,
    material_type character varying(100) NOT NULL,
    required_weight_kg numeric NOT NULL,
    min_price_per_kg numeric,
    max_price_per_kg numeric,
    deadline timestamp with time zone NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_factory_demands" PRIMARY KEY (id),
    CONSTRAINT "FK_factory_demands_factories_factory_id" FOREIGN KEY (factory_id) REFERENCES factories (id) ON DELETE CASCADE
);

CREATE TABLE factory_depot_partnerships (
    id uuid NOT NULL,
    depot_id uuid NOT NULL,
    factory_id uuid NOT NULL,
    status character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_factory_depot_partnerships" PRIMARY KEY (id),
    CONSTRAINT "FK_factory_depot_partnerships_depots_depot_id" FOREIGN KEY (depot_id) REFERENCES depots (id) ON DELETE CASCADE,
    CONSTRAINT "FK_factory_depot_partnerships_factories_factory_id" FOREIGN KEY (factory_id) REFERENCES factories (id) ON DELETE CASCADE
);

CREATE TABLE inventory_batches (
    id uuid NOT NULL,
    depot_id uuid NOT NULL,
    target_factory_id uuid,
    material_type character varying(100) NOT NULL,
    declared_weight_kg numeric NOT NULL,
    description text,
    status character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_inventory_batches" PRIMARY KEY (id),
    CONSTRAINT "FK_inventory_batches_depots_depot_id" FOREIGN KEY (depot_id) REFERENCES depots (id) ON DELETE CASCADE,
    CONSTRAINT "FK_inventory_batches_factories_target_factory_id" FOREIGN KEY (target_factory_id) REFERENCES factories (id)
);

CREATE TABLE pickup_request_items (
    id uuid NOT NULL,
    pickup_request_id uuid NOT NULL,
    material_type character varying(100) NOT NULL,
    weight_kg numeric NOT NULL,
    price_per_kg numeric NOT NULL,
    sub_total numeric NOT NULL,
    CONSTRAINT "PK_pickup_request_items" PRIMARY KEY (id),
    CONSTRAINT "FK_pickup_request_items_pickup_requests_pickup_request_id" FOREIGN KEY (pickup_request_id) REFERENCES pickup_requests (id) ON DELETE CASCADE
);

CREATE TABLE seller_depot_reviews (
    id uuid NOT NULL,
    pickup_request_id uuid NOT NULL,
    depot_id uuid NOT NULL,
    rating integer,
    comment text,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_seller_depot_reviews" PRIMARY KEY (id),
    CONSTRAINT "FK_seller_depot_reviews_depots_depot_id" FOREIGN KEY (depot_id) REFERENCES depots (id) ON DELETE CASCADE,
    CONSTRAINT "FK_seller_depot_reviews_pickup_requests_pickup_request_id" FOREIGN KEY (pickup_request_id) REFERENCES pickup_requests (id) ON DELETE CASCADE
);

CREATE TABLE batch_quality_checks (
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    factory_id uuid NOT NULL,
    actual_weight_kg numeric NOT NULL,
    grade character varying(10) NOT NULL,
    agreed_price_per_kg numeric NOT NULL,
    gross_amount numeric NOT NULL,
    platform_fee_percentage numeric NOT NULL,
    platform_fee_amount numeric NOT NULL,
    net_amount numeric NOT NULL,
    payment_proof_url text,
    is_accepted boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_batch_quality_checks" PRIMARY KEY (id),
    CONSTRAINT "FK_batch_quality_checks_factories_factory_id" FOREIGN KEY (factory_id) REFERENCES factories (id) ON DELETE CASCADE,
    CONSTRAINT "FK_batch_quality_checks_inventory_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES inventory_batches (id) ON DELETE CASCADE
);

CREATE TABLE factory_depot_reviews (
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    factory_id uuid NOT NULL,
    depot_id uuid NOT NULL,
    rating integer,
    comment text,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_factory_depot_reviews" PRIMARY KEY (id),
    CONSTRAINT "FK_factory_depot_reviews_depots_depot_id" FOREIGN KEY (depot_id) REFERENCES depots (id) ON DELETE CASCADE,
    CONSTRAINT "FK_factory_depot_reviews_factories_factory_id" FOREIGN KEY (factory_id) REFERENCES factories (id) ON DELETE CASCADE,
    CONSTRAINT "FK_factory_depot_reviews_inventory_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES inventory_batches (id) ON DELETE CASCADE
);

CREATE TABLE transport_jobs (
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    driver_id uuid,
    status character varying(50) NOT NULL,
    checkin_depot_image_url text,
    checkout_factory_image_url text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_transport_jobs" PRIMARY KEY (id),
    CONSTRAINT "FK_transport_jobs_inventory_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES inventory_batches (id) ON DELETE CASCADE,
    CONSTRAINT "FK_transport_jobs_users_driver_id" FOREIGN KEY (driver_id) REFERENCES users (id)
);

INSERT INTO system_configs (config_key, config_value, description, updated_at)
VALUES ('PLATFORM_FEE_PERCENTAGE', '1.00', 'Phí n?n t?ng 1%', TIMESTAMPTZ '2026-09-20T07:49:15.238039Z');

CREATE UNIQUE INDEX "IX_batch_quality_checks_batch_id" ON batch_quality_checks (batch_id);

CREATE INDEX "IX_batch_quality_checks_factory_id" ON batch_quality_checks (factory_id);

CREATE INDEX "IX_depot_staffs_depot_id" ON depot_staffs (depot_id);

CREATE INDEX "IX_depot_staffs_user_id" ON depot_staffs (user_id);

CREATE INDEX "IX_depots_owner_id" ON depots (owner_id);

CREATE INDEX "IX_factories_owner_id" ON factories (owner_id);

CREATE INDEX "IX_factory_demands_factory_id" ON factory_demands (factory_id);

CREATE UNIQUE INDEX "IX_factory_depot_partnerships_depot_id_factory_id" ON factory_depot_partnerships (depot_id, factory_id);

CREATE INDEX "IX_factory_depot_partnerships_factory_id" ON factory_depot_partnerships (factory_id);

CREATE INDEX "IX_factory_depot_reviews_batch_id" ON factory_depot_reviews (batch_id);

CREATE INDEX "IX_factory_depot_reviews_depot_id" ON factory_depot_reviews (depot_id);

CREATE INDEX "IX_factory_depot_reviews_factory_id" ON factory_depot_reviews (factory_id);

CREATE INDEX "IX_inventory_batches_depot_id" ON inventory_batches (depot_id);

CREATE INDEX "IX_inventory_batches_target_factory_id" ON inventory_batches (target_factory_id);

CREATE INDEX "IX_pickup_request_items_pickup_request_id" ON pickup_request_items (pickup_request_id);

CREATE INDEX "IX_pickup_requests_accepted_collector_id" ON pickup_requests (accepted_collector_id);

CREATE INDEX "IX_pickup_requests_seller_id" ON pickup_requests (seller_id);

CREATE INDEX "IX_pickup_requests_target_depot_id" ON pickup_requests (target_depot_id);

CREATE INDEX "IX_seller_depot_reviews_depot_id" ON seller_depot_reviews (depot_id);

CREATE INDEX "IX_seller_depot_reviews_pickup_request_id" ON seller_depot_reviews (pickup_request_id);

CREATE UNIQUE INDEX "IX_transport_jobs_batch_id" ON transport_jobs (batch_id);

CREATE INDEX "IX_transport_jobs_driver_id" ON transport_jobs (driver_id);

CREATE UNIQUE INDEX "IX_users_email" ON users (email);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260920074916_InitialCreate', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS actual_weight_kg NUMERIC;
ALTER TABLE batch_quality_checks ADD COLUMN IF NOT EXISTS actual_weight_kg NUMERIC;

ALTER TABLE inventory_batches ADD agreed_price_per_kg numeric;

ALTER TABLE inventory_batches ADD factory_decided_at timestamp with time zone;

ALTER TABLE inventory_batches ADD factory_received_at timestamp with time zone;

ALTER TABLE inventory_batches ADD gross_amount numeric;

ALTER TABLE inventory_batches ADD net_amount numeric;

ALTER TABLE inventory_batches ADD payment_reference character varying(200);

ALTER TABLE inventory_batches ADD platform_fee_amount numeric;

ALTER TABLE inventory_batches ADD rejection_reason text;

ALTER TABLE inventory_batches ADD settled_at timestamp with time zone;

ALTER TABLE factory_demands ADD note text;

ALTER TABLE factory_demands ADD updated_at timestamp with time zone NOT NULL DEFAULT (NOW());

ALTER TABLE factories ADD accepted_materials text NOT NULL DEFAULT '';

ALTER TABLE factories ADD business_license_url text;

ALTER TABLE factories ADD capacity_kg_per_month numeric NOT NULL DEFAULT 0.0;

ALTER TABLE factories ADD contact_phone character varying(30);

ALTER TABLE factories ADD environmental_license_url text;

ALTER TABLE factories ADD industrial_zone character varying(200);

ALTER TABLE factories ADD minimum_purity_percent numeric NOT NULL DEFAULT 0.0;

ALTER TABLE factories ADD tax_code character varying(50);

ALTER TABLE batch_quality_checks ADD contamination_percent numeric;

ALTER TABLE batch_quality_checks ADD difference_percentage numeric;

ALTER TABLE batch_quality_checks ADD gross_weight_kg numeric;

ALTER TABLE batch_quality_checks ADD invoice_file_url text;

ALTER TABLE batch_quality_checks ADD invoice_number character varying(100);

ALTER TABLE batch_quality_checks ADD invoice_status character varying(30);

ALTER TABLE batch_quality_checks ADD moisture_percent numeric;

ALTER TABLE batch_quality_checks ADD purity_percent numeric;

ALTER TABLE batch_quality_checks ADD quality_note text;

ALTER TABLE batch_quality_checks ADD resolution character varying(20);

ALTER TABLE batch_quality_checks ADD tare_weight_kg numeric;

ALTER TABLE batch_quality_checks ADD ticket_image_url text;

ALTER TABLE batch_quality_checks ADD ticket_number character varying(100);

CREATE TABLE market_prices (
    id uuid NOT NULL,
    material_type character varying(100) NOT NULL,
    price_per_kg numeric NOT NULL,
    effective_date timestamp with time zone NOT NULL,
    source text,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_market_prices" PRIMARY KEY (id)
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924014347_FactoryWorkflowExtension', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE inventory_batches ADD direct_offer_factory_id uuid;

ALTER TABLE inventory_batches ADD CONSTRAINT "FK_inventory_batches_factories_direct_offer_factory_id" FOREIGN KEY (direct_offer_factory_id) REFERENCES factories (id) ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924014918_FactoryDirectOffers', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE audit_logs (
    id uuid NOT NULL,
    user_id uuid,
    action character varying(100) NOT NULL,
    entity_name character varying(100) NOT NULL,
    entity_id uuid,
    old_data text,
    new_data text,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_audit_logs" PRIMARY KEY (id),
    CONSTRAINT "FK_audit_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE SET NULL
);

ALTER TABLE market_prices ALTER COLUMN source TYPE character varying(255);

CREATE TABLE notifications (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    title character varying(255) NOT NULL,
    message text,
    is_read boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_notifications" PRIMARY KEY (id),
    CONSTRAINT "FK_notifications_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE platform_invoices (
    id uuid NOT NULL,
    payer_id uuid NOT NULL,
    period_year integer NOT NULL,
    period_month integer NOT NULL,
    total_fee_amount numeric NOT NULL,
    status character varying(20) NOT NULL,
    paid_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_platform_invoices" PRIMARY KEY (id),
    CONSTRAINT "FK_platform_invoices_users_payer_id" FOREIGN KEY (payer_id) REFERENCES users (id) ON DELETE RESTRICT
);

UPDATE system_configs SET description = 'Phí nền tảng 1%', updated_at = TIMESTAMPTZ '2026-09-24T02:13:01.666535Z'
WHERE config_key = 'PLATFORM_FEE_PERCENTAGE';

CREATE INDEX "IX_audit_logs_user_id" ON audit_logs (user_id);

CREATE INDEX "IX_notifications_user_id" ON notifications (user_id);

CREATE UNIQUE INDEX "IX_platform_invoices_payer_id_period_year_period_month" ON platform_invoices (payer_id, period_year, period_month);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924021303_AddAdminFeatures', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE UNIQUE INDEX "IX_platform_transactions_source_type_source_id" ON platform_transactions (source_type, source_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924064439_DepotPaymentUniqueness', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE depots ADD contact_phone character varying(20);

ALTER TABLE depots ADD description text;

ALTER TABLE depots ADD tax_code text;

CREATE TABLE platform_fee_invoices (
    id uuid NOT NULL,
    owner_id uuid NOT NULL,
    period_start date NOT NULL,
    amount numeric NOT NULL,
    status text NOT NULL,
    payment_proof_url text,
    created_at timestamp with time zone NOT NULL,
    submitted_at timestamp with time zone,
    CONSTRAINT "PK_platform_fee_invoices" PRIMARY KEY (id),
    CONSTRAINT "CK_fee_invoice_amount" CHECK (amount >= 0),
    CONSTRAINT "CK_fee_invoice_status" CHECK (status IN ('UNPAID', 'SUBMITTED', 'PAID')),
    CONSTRAINT "FK_platform_fee_invoices_users_owner_id" FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE RESTRICT
);

UPDATE system_configs SET config_value = '5.00', description = 'Phí nền tảng mặc định 5%' WHERE config_key = 'PLATFORM_FEE_PERCENTAGE' AND config_value = '1.00';

CREATE UNIQUE INDEX "IX_platform_fee_invoices_owner_id_period_start" ON platform_fee_invoices (owner_id, period_start);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924131224_DepotProfileAndFeeInvoices', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE SEQUENCE depot_batch_number START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;

ALTER TABLE inventory_batches ADD code character varying(40);

CREATE UNIQUE INDEX "IX_inventory_batches_code" ON inventory_batches (code);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924133220_DepotBatchCodes', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE users ADD avatar_url character varying(2048);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260927154743_AddStaffAvatar', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE platform_invoices ADD payment_proof_url text;

ALTER TABLE platform_invoices ADD submitted_at timestamp with time zone;

DO $$ BEGIN
    IF EXISTS (
        SELECT 1 FROM platform_fee_invoices old
        JOIN platform_invoices current ON current.payer_id = old.owner_id
            AND current.period_year = EXTRACT(YEAR FROM old.period_start)::int
            AND current.period_month = EXTRACT(MONTH FROM old.period_start)::int
        WHERE current.total_fee_amount <> old.amount
    ) THEN
        RAISE EXCEPTION 'Hoa don Depot/Admin cung ky khac so tien: can doi soat truoc khi chuyen du lieu';
    END IF;
END $$;
INSERT INTO platform_invoices
    (id, payer_id, period_year, period_month, total_fee_amount, status, created_at, payment_proof_url, submitted_at)
SELECT id, owner_id, EXTRACT(YEAR FROM period_start)::int, EXTRACT(MONTH FROM period_start)::int,
    amount, CASE WHEN status = 'UNPAID' THEN 'PENDING' ELSE status END, created_at, payment_proof_url, submitted_at
FROM platform_fee_invoices
ON CONFLICT (payer_id, period_year, period_month) DO UPDATE SET
    payment_proof_url = COALESCE(platform_invoices.payment_proof_url, EXCLUDED.payment_proof_url),
    submitted_at = COALESCE(platform_invoices.submitted_at, EXCLUDED.submitted_at),
    status = CASE WHEN platform_invoices.status = 'PAID' OR EXCLUDED.status = 'PAID' THEN 'PAID'
        WHEN platform_invoices.status = 'SUBMITTED' OR EXCLUDED.status = 'SUBMITTED' THEN 'SUBMITTED'
        ELSE platform_invoices.status END;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929015907_DepotAdminInvoiceProof', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE factory_depot_partnerships ADD blocked_by_depot boolean NOT NULL DEFAULT FALSE;

ALTER TABLE factory_depot_partnerships ADD blocked_by_factory boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260930114738_DepotPartnershipBlocking', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE inventory_batches ADD image_urls text[] NOT NULL DEFAULT ARRAY[]::text[];

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261001135514_DepotBatchMaterialPhotos', '8.0.10');

COMMIT;

