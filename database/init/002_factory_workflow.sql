-- Safe to run more than once. Adds fields required by the Factory workflow
-- to databases created from older project snapshots.
ALTER TABLE IF EXISTS "Factories"
    ADD COLUMN IF NOT EXISTS "BusinessLicenseUrl" text,
    ADD COLUMN IF NOT EXISTS "EnvironmentalLicenseUrl" text,
    ADD COLUMN IF NOT EXISTS "CapacityKgPerMonth" numeric NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "MinimumPurityPercent" numeric NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "AcceptedMaterials" text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Latitude" numeric,
    ADD COLUMN IF NOT EXISTS "Longitude" numeric;

ALTER TABLE IF EXISTS "FactoryDemands"
    ADD COLUMN IF NOT EXISTS "PricePerKg" numeric,
    ADD COLUMN IF NOT EXISTS "MinPricePerKg" numeric,
    ADD COLUMN IF NOT EXISTS "Note" text,
    ADD COLUMN IF NOT EXISTS "Deadline" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now();

ALTER TABLE IF EXISTS "BatchOrders"
    ADD COLUMN IF NOT EXISTS "TotalAmount" numeric,
    ADD COLUMN IF NOT EXISTS "Version" uuid NOT NULL DEFAULT gen_random_uuid(),
    ADD COLUMN IF NOT EXISTS "ReceivedAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "DecidedAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "RejectionReason" text,
    ADD COLUMN IF NOT EXISTS "SettledAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "FeeAmount" numeric,
    ADD COLUMN IF NOT EXISTS "NetPayableAmount" numeric,
    ADD COLUMN IF NOT EXISTS "PaymentReference" text;

ALTER TABLE IF EXISTS "InventoryBatches"
    ADD COLUMN IF NOT EXISTS "TargetFactoryId" uuid,
    ADD COLUMN IF NOT EXISTS "Version" uuid NOT NULL DEFAULT gen_random_uuid();

ALTER TABLE IF EXISTS "WeightVerifications"
    ADD COLUMN IF NOT EXISTS "PurityPercent" numeric,
    ADD COLUMN IF NOT EXISTS "MoisturePercent" numeric,
    ADD COLUMN IF NOT EXISTS "ContaminationPercent" numeric,
    ADD COLUMN IF NOT EXISTS "Grade" text,
    ADD COLUMN IF NOT EXISTS "QualityNote" text,
    ADD COLUMN IF NOT EXISTS "Note" text;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Factories_UserId" ON "Factories" ("UserId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_BatchOrders_BatchId" ON "BatchOrders" ("BatchId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Partnerships_FactoryId_DepotId" ON "Partnerships" ("FactoryId", "DepotId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WeightVerifications_BatchOrderId" ON "WeightVerifications" ("BatchOrderId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WeightTickets_BatchOrderId" ON "WeightTickets" ("BatchOrderId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Invoices_BatchOrderId" ON "Invoices" ("BatchOrderId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlatformFeeLogs_BatchOrderId" ON "PlatformFeeLogs" ("BatchOrderId") WHERE "BatchOrderId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260923070000_FactoryWorkflow', '8.0.11')
ON CONFLICT ("MigrationId") DO NOTHING;
