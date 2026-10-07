-- Hợp nhất dữ liệu từ các bảng PascalCase đời đầu vào schema snake_case dùng chung,
-- kiểm tra dữ liệu đã chuyển rồi mới xóa bảng legacy.
-- Chạy đúng database PostgreSQL; toàn bộ thay đổi nằm trong một transaction.

BEGIN;

ALTER TABLE public.platform_transactions
    ADD COLUMN IF NOT EXISTS payer_id UUID,
    ADD COLUMN IF NOT EXISTS transaction_amount DECIMAL(18, 2),
    ADD COLUMN IF NOT EXISTS fee_percentage DECIMAL(5, 2);

DO $$
BEGIN
    -- Trùng email chỉ được gộp khi role, phone và trạng thái tài khoản khớp.
    IF EXISTS (
        SELECT 1
        FROM public."Users" legacy
        JOIN public.users current ON lower(current.email) = lower(legacy."Email")
        WHERE legacy."Role" <> current.role
           OR legacy."Phone" IS DISTINCT FROM current.phone
           OR legacy."IsActive" <> current.is_active
    ) THEN
        RAISE EXCEPTION 'Khong the gop Users legacy: email trung nhung role/phone/trang thai khac';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."Users" legacy
        JOIN public.users current ON current.id = legacy."Id"
        WHERE lower(current.email) <> lower(legacy."Email")
    ) THEN
        RAISE EXCEPTION 'Khong the gop Users legacy: UUID da thuoc email khac';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."Factories" legacy
        JOIN public.factories current ON current.id = legacy."Id"
    ) THEN
        RAISE EXCEPTION 'Khong the gop Factories legacy: trung UUID';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."FactoryDemands" legacy
        JOIN public.factory_demands current ON current.id = legacy."Id"
    ) THEN
        RAISE EXCEPTION 'Khong the gop FactoryDemands legacy: trung UUID';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."FactoryDemands"
        WHERE "MaterialType" < 0 OR "MaterialType" > 10
           OR "Deadline" IS NULL OR "QuantityKg" <= 0
    ) THEN
        RAISE EXCEPTION 'FactoryDemands legacy co material/deadline/quantity khong hop le';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."PlatformFeeLogs"
        WHERE ("PickupRequestId" IS NULL) = ("BatchOrderId" IS NULL)
    ) THEN
        RAISE EXCEPTION 'PlatformFeeLogs legacy phai co dung mot source id';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public."PlatformFeeLogs" legacy
        JOIN public.platform_transactions current
          ON current.source_type = CASE
                WHEN legacy."PickupRequestId" IS NOT NULL THEN 'PICKUP_REQUEST'
                ELSE 'BATCH_ORDER'
             END
         AND current.source_id = COALESCE(legacy."PickupRequestId", legacy."BatchOrderId")
    ) THEN
        RAISE EXCEPTION 'PlatformFeeLogs legacy trung source voi platform_transactions';
    END IF;
END $$;

-- Bổ sung user legacy chưa có trong bảng gốc. User trùng email được dùng ID hiện hành.
INSERT INTO public.users
    (id, email, password_hash, role, full_name, phone, is_active, created_at, updated_at)
SELECT legacy."Id", legacy."Email", legacy."PasswordHash", legacy."Role",
       legacy."FullName", legacy."Phone", legacy."IsActive", legacy."CreatedAt", legacy."UpdatedAt"
FROM public."Users" legacy
WHERE NOT EXISTS (SELECT 1 FROM public.users current WHERE current.id = legacy."Id")
  AND NOT EXISTS (SELECT 1 FROM public.users current WHERE lower(current.email) = lower(legacy."Email"));

-- Gộp hồ sơ Factory, ánh xạ chủ sở hữu bằng UUID hoặc email đã đối soát.
INSERT INTO public.factories
    (id, owner_id, name, address, latitude, longitude, rating, created_at,
     tax_code, industrial_zone, contact_phone, business_license_url,
     environmental_license_url, capacity_kg_per_month,
     minimum_purity_percent, accepted_materials)
SELECT legacy."Id", canonical_user.id, legacy."CompanyName", COALESCE(legacy."Address", ''),
       legacy."Latitude", legacy."Longitude", 0, legacy."CreatedAt",
       legacy."TaxCode", legacy."IndustrialZone", legacy."ContactPhone",
       legacy."BusinessLicenseUrl", legacy."EnvironmentalLicenseUrl",
       legacy."CapacityKgPerMonth", legacy."MinimumPurityPercent", legacy."AcceptedMaterials"
FROM public."Factories" legacy
JOIN public."Users" legacy_user ON legacy_user."Id" = legacy."UserId"
JOIN public.users canonical_user
  ON canonical_user.id = legacy_user."Id"
  OR lower(canonical_user.email) = lower(legacy_user."Email");

-- MaterialType legacy là enum số theo thứ tự của MaterialType trong ứng dụng.
INSERT INTO public.factory_demands
    (id, factory_id, material_type, required_weight_kg,
     min_price_per_kg, max_price_per_kg, deadline, is_active,
     created_at, note, updated_at)
SELECT legacy."Id", legacy."FactoryId",
       (ARRAY['PET','HDPE','PVC','PAPER','CARDBOARD','ALUMINUM','IRON','STEEL','COPPER','ELECTRONIC_WASTE','OTHER'])[legacy."MaterialType" + 1],
       legacy."QuantityKg", legacy."MinPricePerKg", legacy."PricePerKg",
       legacy."Deadline", legacy."IsActive", legacy."CreatedAt", legacy."Note", legacy."CreatedAt"
FROM public."FactoryDemands" legacy;

-- Giữ đầy đủ số tiền, tỷ lệ, người trả và source id của log phí legacy.
INSERT INTO public.platform_transactions
    (id, source_type, source_id, fee_amount, payer_id, transaction_amount,
     fee_percentage, description, created_at)
SELECT legacy."Id",
       CASE WHEN legacy."PickupRequestId" IS NOT NULL THEN 'PICKUP_REQUEST' ELSE 'BATCH_ORDER' END,
       COALESCE(legacy."PickupRequestId", legacy."BatchOrderId"),
       legacy."FeeAmount", legacy."PayerId", legacy."TransactionAmount",
       legacy."FeePercentage", 'Chuyen tu PlatformFeeLogs legacy', legacy."CreatedAt"
FROM public."PlatformFeeLogs" legacy;

DO $$
DECLARE
    table_name text;
    row_count bigint;
    legacy_tables text[] := ARRAY[
        'AuditLogs','EprCertificates','PlatformFeeLogs','Invoices','WeightTickets',
        'WeightVerifications','TransportTrackingLogs','TransportJobs','BatchOrders',
        'BatchImages','InventoryBatches','PickupRequestImages','PickupRequestItems',
        'PickupRequests','Partnerships','FactoryDemands','MarketPrices','Notifications',
        'DepotEmployees','Drivers','Sellers','Factories','Depots','Users'
    ];
BEGIN
    IF EXISTS (
        SELECT 1 FROM public."Users" legacy
        WHERE NOT EXISTS (
            SELECT 1 FROM public.users current
            WHERE current.id = legacy."Id"
               OR lower(current.email) = lower(legacy."Email")
        )
    ) THEN
        RAISE EXCEPTION 'Con Users legacy chua duoc chuyen';
    END IF;

    IF EXISTS (SELECT 1 FROM public."Factories" legacy WHERE NOT EXISTS
        (SELECT 1 FROM public.factories current WHERE current.id = legacy."Id")) THEN
        RAISE EXCEPTION 'Con Factories legacy chua duoc chuyen';
    END IF;

    IF EXISTS (SELECT 1 FROM public."FactoryDemands" legacy WHERE NOT EXISTS
        (SELECT 1 FROM public.factory_demands current WHERE current.id = legacy."Id")) THEN
        RAISE EXCEPTION 'Con FactoryDemands legacy chua duoc chuyen';
    END IF;

    IF EXISTS (SELECT 1 FROM public."PlatformFeeLogs" legacy WHERE NOT EXISTS
        (SELECT 1 FROM public.platform_transactions current WHERE current.id = legacy."Id")) THEN
        RAISE EXCEPTION 'Con PlatformFeeLogs legacy chua duoc chuyen';
    END IF;

    FOREACH table_name IN ARRAY legacy_tables LOOP
        IF table_name NOT IN ('Users','Factories','FactoryDemands','PlatformFeeLogs')
           AND to_regclass(format('public.%I', table_name)) IS NOT NULL THEN
            EXECUTE format('SELECT count(*) FROM public.%I', table_name) INTO row_count;
            IF row_count > 0 THEN
                RAISE EXCEPTION 'Bang legacy % con % dong; dung de tranh mat du lieu', table_name, row_count;
            END IF;
        END IF;
    END LOOP;
END $$;

-- Xóa từ bảng con lên bảng cha sau khi toàn bộ dữ liệu đã được xác nhận.
DROP TABLE IF EXISTS public."AuditLogs";
DROP TABLE IF EXISTS public."EprCertificates";
DROP TABLE IF EXISTS public."PlatformFeeLogs";
DROP TABLE IF EXISTS public."Invoices";
DROP TABLE IF EXISTS public."WeightTickets";
DROP TABLE IF EXISTS public."WeightVerifications";
DROP TABLE IF EXISTS public."TransportTrackingLogs";
DROP TABLE IF EXISTS public."TransportJobs";
DROP TABLE IF EXISTS public."BatchOrders";
DROP TABLE IF EXISTS public."BatchImages";
DROP TABLE IF EXISTS public."InventoryBatches";
DROP TABLE IF EXISTS public."PickupRequestImages";
DROP TABLE IF EXISTS public."PickupRequestItems";
DROP TABLE IF EXISTS public."PickupRequests";
DROP TABLE IF EXISTS public."Partnerships";
DROP TABLE IF EXISTS public."FactoryDemands";
DROP TABLE IF EXISTS public."MarketPrices";
DROP TABLE IF EXISTS public."Notifications";
DROP TABLE IF EXISTS public."DepotEmployees";
DROP TABLE IF EXISTS public."Drivers";
DROP TABLE IF EXISTS public."Sellers";
DROP TABLE IF EXISTS public."Factories";
DROP TABLE IF EXISTS public."Depots";
DROP TABLE IF EXISTS public."Users";

COMMIT;
