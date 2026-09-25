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

