CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "AuditLogs" (
        "Id" uuid NOT NULL,
        "UserId" uuid,
        "Action" text,
        "EntityName" text,
        "EntityId" uuid,
        "OldData" text,
        "NewData" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "MarketPrices" (
        "Id" uuid NOT NULL,
        "MaterialType" integer NOT NULL,
        "PricePerKg" numeric NOT NULL,
        "EffectiveDate" timestamp with time zone NOT NULL,
        "Source" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_MarketPrices" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "PlatformFeeLogs" (
        "Id" uuid NOT NULL,
        "PickupRequestId" uuid,
        "BatchOrderId" uuid,
        "PayerId" uuid NOT NULL,
        "TransactionAmount" numeric NOT NULL,
        "FeePercentage" numeric NOT NULL,
        "FeeAmount" numeric NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PlatformFeeLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "Email" text NOT NULL,
        "PasswordHash" text NOT NULL,
        "FullName" text NOT NULL,
        "Phone" text,
        "Role" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Depots" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CompanyName" text NOT NULL,
        "TaxCode" text,
        "Address" text,
        "City" text,
        "Latitude" numeric,
        "Longitude" numeric,
        "ContactPhone" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Depots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Depots_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Factories" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CompanyName" text NOT NULL,
        "TaxCode" text,
        "Address" text,
        "IndustrialZone" text,
        "ContactPhone" text,
        "BusinessLicenseUrl" text,
        "EnvironmentalLicenseUrl" text,
        "CapacityKgPerMonth" numeric NOT NULL,
        "MinimumPurityPercent" numeric NOT NULL,
        "AcceptedMaterials" text NOT NULL,
        "Latitude" numeric,
        "Longitude" numeric,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Factories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Factories_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Notifications" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Title" text NOT NULL,
        "Message" text,
        "IsRead" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Sellers" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Address" text,
        "Latitude" numeric,
        "Longitude" numeric,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Sellers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Sellers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "DepotEmployees" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "DepotId" uuid NOT NULL,
        "EmployeeCode" text,
        "TotalPickupsCompleted" integer NOT NULL,
        "TotalKgCollected" numeric NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_DepotEmployees" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DepotEmployees_Depots_DepotId" FOREIGN KEY ("DepotId") REFERENCES "Depots" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_DepotEmployees_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Drivers" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "DepotId" uuid NOT NULL,
        "LicenseNumber" text,
        "VehiclePlate" text,
        "VehicleType" text,
        "MaxCapacityKg" numeric,
        "IsAvailable" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Drivers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Drivers_Depots_DepotId" FOREIGN KEY ("DepotId") REFERENCES "Depots" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Drivers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "FactoryDemands" (
        "Id" uuid NOT NULL,
        "FactoryId" uuid NOT NULL,
        "MaterialType" integer NOT NULL,
        "QuantityKg" numeric NOT NULL,
        "PricePerKg" numeric,
        "MinPricePerKg" numeric,
        "Note" text,
        "Deadline" timestamp with time zone,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_FactoryDemands" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_FactoryDemands_Factories_FactoryId" FOREIGN KEY ("FactoryId") REFERENCES "Factories" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "InventoryBatches" (
        "Id" uuid NOT NULL,
        "DepotId" uuid NOT NULL,
        "TargetFactoryId" uuid,
        "Version" uuid NOT NULL,
        "BatchCode" text NOT NULL,
        "MaterialType" text NOT NULL,
        "EstimatedWeightKg" numeric NOT NULL,
        "ActualWeightKg" numeric,
        "UnitPrice" numeric,
        "Description" text,
        "ThumbnailImageUrl" text,
        "Status" text NOT NULL,
        "TransportType" integer,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_InventoryBatches" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InventoryBatches_Depots_DepotId" FOREIGN KEY ("DepotId") REFERENCES "Depots" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_InventoryBatches_Factories_TargetFactoryId" FOREIGN KEY ("TargetFactoryId") REFERENCES "Factories" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Partnerships" (
        "Id" uuid NOT NULL,
        "DepotId" uuid NOT NULL,
        "FactoryId" uuid NOT NULL,
        "Status" text NOT NULL,
        "Rating" numeric,
        "Comment" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Partnerships" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Partnerships_Depots_DepotId" FOREIGN KEY ("DepotId") REFERENCES "Depots" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Partnerships_Factories_FactoryId" FOREIGN KEY ("FactoryId") REFERENCES "Factories" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "PickupRequests" (
        "Id" uuid NOT NULL,
        "SellerId" uuid NOT NULL,
        "DepotId" uuid,
        "AssignedEmployeeId" uuid,
        "Description" text,
        "Address" text,
        "Latitude" numeric,
        "Longitude" numeric,
        "ScheduledDate" timestamp with time zone,
        "TimeSlot" text,
        "IsBroadcast" boolean NOT NULL,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PickupRequests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PickupRequests_DepotEmployees_AssignedEmployeeId" FOREIGN KEY ("AssignedEmployeeId") REFERENCES "DepotEmployees" ("Id"),
        CONSTRAINT "FK_PickupRequests_Depots_DepotId" FOREIGN KEY ("DepotId") REFERENCES "Depots" ("Id"),
        CONSTRAINT "FK_PickupRequests_Sellers_SellerId" FOREIGN KEY ("SellerId") REFERENCES "Sellers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "BatchImages" (
        "Id" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "ImageUrl" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_BatchImages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_BatchImages_InventoryBatches_BatchId" FOREIGN KEY ("BatchId") REFERENCES "InventoryBatches" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "BatchOrders" (
        "Id" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "FactoryId" uuid NOT NULL,
        "AgreedPrice" numeric NOT NULL,
        "TotalAmount" numeric,
        "Version" uuid NOT NULL,
        "ReceivedAt" timestamp with time zone,
        "DecidedAt" timestamp with time zone,
        "RejectionReason" text,
        "SettledAt" timestamp with time zone,
        "FeeAmount" numeric,
        "NetPayableAmount" numeric,
        "PaymentReference" text,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_BatchOrders" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_BatchOrders_Factories_FactoryId" FOREIGN KEY ("FactoryId") REFERENCES "Factories" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_BatchOrders_InventoryBatches_BatchId" FOREIGN KEY ("BatchId") REFERENCES "InventoryBatches" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "PickupRequestImages" (
        "Id" uuid NOT NULL,
        "PickupRequestId" uuid NOT NULL,
        "ImageUrl" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PickupRequestImages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PickupRequestImages_PickupRequests_PickupRequestId" FOREIGN KEY ("PickupRequestId") REFERENCES "PickupRequests" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "PickupRequestItems" (
        "Id" uuid NOT NULL,
        "PickupRequestId" uuid NOT NULL,
        "MaterialType" text NOT NULL,
        "MaterialLabel" text,
        "WeightKg" numeric NOT NULL,
        "PricePerKg" numeric NOT NULL,
        "TotalAmount" numeric NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PickupRequestItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PickupRequestItems_PickupRequests_PickupRequestId" FOREIGN KEY ("PickupRequestId") REFERENCES "PickupRequests" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "EprCertificates" (
        "Id" uuid NOT NULL,
        "BatchOrderId" uuid NOT NULL,
        "CertificateCode" text NOT NULL,
        "HashValue" text NOT NULL,
        "MaterialType" integer NOT NULL,
        "CertifiedWeightKg" numeric,
        "IssuedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_EprCertificates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_EprCertificates_BatchOrders_BatchOrderId" FOREIGN KEY ("BatchOrderId") REFERENCES "BatchOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "Invoices" (
        "Id" uuid NOT NULL,
        "BatchOrderId" uuid NOT NULL,
        "InvoiceNumber" text,
        "InvoiceFileUrl" text,
        "Subtotal" numeric,
        "VatAmount" numeric,
        "TotalAmount" numeric,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Invoices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Invoices_BatchOrders_BatchOrderId" FOREIGN KEY ("BatchOrderId") REFERENCES "BatchOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "TransportJobs" (
        "Id" uuid NOT NULL,
        "BatchOrderId" uuid NOT NULL,
        "DriverId" uuid,
        "PickupAddress" text NOT NULL,
        "DeliveryAddress" text NOT NULL,
        "TransportFee" numeric,
        "Status" text NOT NULL,
        "PickupTime" timestamp with time zone,
        "DeliveredTime" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TransportJobs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TransportJobs_BatchOrders_BatchOrderId" FOREIGN KEY ("BatchOrderId") REFERENCES "BatchOrders" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TransportJobs_Drivers_DriverId" FOREIGN KEY ("DriverId") REFERENCES "Drivers" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "WeightTickets" (
        "Id" uuid NOT NULL,
        "BatchOrderId" uuid NOT NULL,
        "TicketNumber" text,
        "GrossWeightKg" numeric,
        "TareWeightKg" numeric,
        "NetWeightKg" numeric,
        "TicketImageUrl" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_WeightTickets" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WeightTickets_BatchOrders_BatchOrderId" FOREIGN KEY ("BatchOrderId") REFERENCES "BatchOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "WeightVerifications" (
        "Id" uuid NOT NULL,
        "BatchOrderId" uuid NOT NULL,
        "DepotWeightKg" numeric NOT NULL,
        "FactoryWeightKg" numeric NOT NULL,
        "DifferencePercentage" numeric NOT NULL,
        "IsVerified" boolean NOT NULL,
        "PurityPercent" numeric,
        "MoisturePercent" numeric,
        "ContaminationPercent" numeric,
        "Grade" text,
        "QualityNote" text,
        "Note" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_WeightVerifications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WeightVerifications_BatchOrders_BatchOrderId" FOREIGN KEY ("BatchOrderId") REFERENCES "BatchOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE TABLE "TransportTrackingLogs" (
        "Id" uuid NOT NULL,
        "TransportJobId" uuid NOT NULL,
        "Latitude" numeric,
        "Longitude" numeric,
        "Note" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TransportTrackingLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TransportTrackingLogs_TransportJobs_TransportJobId" FOREIGN KEY ("TransportJobId") REFERENCES "TransportJobs" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_BatchImages_BatchId" ON "BatchImages" ("BatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_BatchOrders_BatchId" ON "BatchOrders" ("BatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_BatchOrders_FactoryId" ON "BatchOrders" ("FactoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_DepotEmployees_DepotId" ON "DepotEmployees" ("DepotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_DepotEmployees_UserId" ON "DepotEmployees" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_Depots_UserId" ON "Depots" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_Drivers_DepotId" ON "Drivers" ("DepotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_Drivers_UserId" ON "Drivers" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_EprCertificates_BatchOrderId" ON "EprCertificates" ("BatchOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_EprCertificates_CertificateCode" ON "EprCertificates" ("CertificateCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Factories_UserId" ON "Factories" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_FactoryDemands_FactoryId" ON "FactoryDemands" ("FactoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_InventoryBatches_BatchCode" ON "InventoryBatches" ("BatchCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_InventoryBatches_DepotId" ON "InventoryBatches" ("DepotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_InventoryBatches_TargetFactoryId" ON "InventoryBatches" ("TargetFactoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Invoices_BatchOrderId" ON "Invoices" ("BatchOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_Notifications_UserId" ON "Notifications" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_Partnerships_DepotId" ON "Partnerships" ("DepotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Partnerships_FactoryId_DepotId" ON "Partnerships" ("FactoryId", "DepotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_PickupRequestImages_PickupRequestId" ON "PickupRequestImages" ("PickupRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_PickupRequestItems_PickupRequestId" ON "PickupRequestItems" ("PickupRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_PickupRequests_AssignedEmployeeId" ON "PickupRequests" ("AssignedEmployeeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_PickupRequests_DepotId" ON "PickupRequests" ("DepotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_PickupRequests_SellerId" ON "PickupRequests" ("SellerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_PlatformFeeLogs_BatchOrderId" ON "PlatformFeeLogs" ("BatchOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_Sellers_UserId" ON "Sellers" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_TransportJobs_BatchOrderId" ON "TransportJobs" ("BatchOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_TransportJobs_DriverId" ON "TransportJobs" ("DriverId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE INDEX "IX_TransportTrackingLogs_TransportJobId" ON "TransportTrackingLogs" ("TransportJobId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_WeightTickets_BatchOrderId" ON "WeightTickets" ("BatchOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_WeightVerifications_BatchOrderId" ON "WeightVerifications" ("BatchOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923052033_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260923052033_InitialCreate', '8.0.11');
    END IF;
END $EF$;
COMMIT;

