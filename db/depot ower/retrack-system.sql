-- RETRACK — SCHEMA TỔNG HỢP TOÀN HỆ THỐNG
-- Người tổng hợp: Ngô Sỹ Giá (Depot Owner). Cập nhật: 30/09/2026, UTC+7.
-- Phạm vi: toàn bộ 9 EF migrations trong nhánh hiện tại, từ InitialCreate
-- đến 20260929015907_DepotAdminInvoiceProof; không chỉ riêng role Depot.
-- Đây là SQL triển khai sinh từ migrations, không chuyển dự án thành Database First.
-- Dùng cho database PostgreSQL đã tạo rỗng hoặc có lịch sử EF migrations tương thích.
-- Database tạo bằng SQL cũ nhưng thiếu __EFMigrationsHistory phải đối chiếu/baseline trước.
-- Không chạy đồng thời với tiến trình khác đang migrate; dừng ngay nếu có lỗi.
-- Không chứa thông tin đăng nhập, tài khoản demo hay giao dịch kiểm thử.
-- Các cấu hình nền tảng đi kèm migration được giữ nguyên theo lịch sử của dự án.
-- Ví dụ (psql hỏi mật khẩu; thay tên DB sau khi kiểm tra đúng môi trường):
-- psql -h localhost -U postgres -d TEN_DATABASE -W -v ON_ERROR_STOP=1 -f "db/depot ower/retrack-system.sql"
-- Sinh lại sau khi thêm migration (build trước; không sửa thủ công phần sinh tự động):
-- dotnet ef migrations script --idempotent --project backend/Retrack.API --output "db/depot ower/retrack-system.sql"
-- Lưu ý: lệnh sinh lại ghi đè file, cần giữ phần hướng dẫn này khi bàn giao.
-- Đã kiểm tra các câu lệnh hai lượt trên schema cô lập ở Retrack_TV2_test,
-- gộp transaction để rollback toàn bộ: 9 migration, 21 bảng; không sửa DB ứng dụng.

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE TABLE platform_transactions (
        id uuid NOT NULL,
        source_type character varying(50) NOT NULL,
        source_id uuid NOT NULL,
        fee_amount numeric NOT NULL,
        description text,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_platform_transactions" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE TABLE system_configs (
        config_key character varying(50) NOT NULL,
        config_value text NOT NULL,
        description text,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_system_configs" PRIMARY KEY (config_key)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    INSERT INTO system_configs (config_key, config_value, description, updated_at)
    VALUES ('PLATFORM_FEE_PERCENTAGE', '1.00', 'Phí n?n t?ng 1%', TIMESTAMPTZ '2026-09-20T07:49:15.238039Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_batch_quality_checks_batch_id" ON batch_quality_checks (batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_batch_quality_checks_factory_id" ON batch_quality_checks (factory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_depot_staffs_depot_id" ON depot_staffs (depot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_depot_staffs_user_id" ON depot_staffs (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_depots_owner_id" ON depots (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_factories_owner_id" ON factories (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_factory_demands_factory_id" ON factory_demands (factory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_factory_depot_partnerships_depot_id_factory_id" ON factory_depot_partnerships (depot_id, factory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_factory_depot_partnerships_factory_id" ON factory_depot_partnerships (factory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_factory_depot_reviews_batch_id" ON factory_depot_reviews (batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_factory_depot_reviews_depot_id" ON factory_depot_reviews (depot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_factory_depot_reviews_factory_id" ON factory_depot_reviews (factory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_inventory_batches_depot_id" ON inventory_batches (depot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_inventory_batches_target_factory_id" ON inventory_batches (target_factory_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_pickup_request_items_pickup_request_id" ON pickup_request_items (pickup_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_pickup_requests_accepted_collector_id" ON pickup_requests (accepted_collector_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_pickup_requests_seller_id" ON pickup_requests (seller_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_pickup_requests_target_depot_id" ON pickup_requests (target_depot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_seller_depot_reviews_depot_id" ON seller_depot_reviews (depot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_seller_depot_reviews_pickup_request_id" ON seller_depot_reviews (pickup_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_transport_jobs_batch_id" ON transport_jobs (batch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE INDEX "IX_transport_jobs_driver_id" ON transport_jobs (driver_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_email" ON users (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920074916_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920074916_InitialCreate', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS actual_weight_kg NUMERIC;
    ALTER TABLE batch_quality_checks ADD COLUMN IF NOT EXISTS actual_weight_kg NUMERIC;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD agreed_price_per_kg numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD factory_decided_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD factory_received_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD gross_amount numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD net_amount numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD payment_reference character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD platform_fee_amount numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD rejection_reason text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE inventory_batches ADD settled_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factory_demands ADD note text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factory_demands ADD updated_at timestamp with time zone NOT NULL DEFAULT (NOW());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD accepted_materials text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD business_license_url text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD capacity_kg_per_month numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD contact_phone character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD environmental_license_url text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD industrial_zone character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD minimum_purity_percent numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE factories ADD tax_code character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD contamination_percent numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD difference_percentage numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD gross_weight_kg numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD invoice_file_url text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD invoice_number character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD invoice_status character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD moisture_percent numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD purity_percent numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD quality_note text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD resolution character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD tare_weight_kg numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD ticket_image_url text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    ALTER TABLE batch_quality_checks ADD ticket_number character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    CREATE TABLE market_prices (
        id uuid NOT NULL,
        material_type character varying(100) NOT NULL,
        price_per_kg numeric NOT NULL,
        effective_date timestamp with time zone NOT NULL,
        source text,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_market_prices" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014347_FactoryWorkflowExtension') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924014347_FactoryWorkflowExtension', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014918_FactoryDirectOffers') THEN
    ALTER TABLE inventory_batches ADD direct_offer_factory_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014918_FactoryDirectOffers') THEN
    ALTER TABLE inventory_batches ADD CONSTRAINT "FK_inventory_batches_factories_direct_offer_factory_id" FOREIGN KEY (direct_offer_factory_id) REFERENCES factories (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924014918_FactoryDirectOffers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924014918_FactoryDirectOffers', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
    ALTER TABLE market_prices ALTER COLUMN source TYPE character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
    UPDATE system_configs SET description = 'Phí nền tảng 1%', updated_at = TIMESTAMPTZ '2026-09-24T02:13:01.666535Z'
    WHERE config_key = 'PLATFORM_FEE_PERCENTAGE';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
    CREATE INDEX "IX_audit_logs_user_id" ON audit_logs (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
    CREATE INDEX "IX_notifications_user_id" ON notifications (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
    CREATE UNIQUE INDEX "IX_platform_invoices_payer_id_period_year_period_month" ON platform_invoices (payer_id, period_year, period_month);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924021303_AddAdminFeatures') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924021303_AddAdminFeatures', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924064439_DepotPaymentUniqueness') THEN
    CREATE UNIQUE INDEX "IX_platform_transactions_source_type_source_id" ON platform_transactions (source_type, source_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924064439_DepotPaymentUniqueness') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924064439_DepotPaymentUniqueness', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924131224_DepotProfileAndFeeInvoices') THEN
    ALTER TABLE depots ADD contact_phone character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924131224_DepotProfileAndFeeInvoices') THEN
    ALTER TABLE depots ADD description text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924131224_DepotProfileAndFeeInvoices') THEN
    ALTER TABLE depots ADD tax_code text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924131224_DepotProfileAndFeeInvoices') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924131224_DepotProfileAndFeeInvoices') THEN
    UPDATE system_configs SET config_value = '5.00', description = 'Phí nền tảng mặc định 5%' WHERE config_key = 'PLATFORM_FEE_PERCENTAGE' AND config_value = '1.00';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924131224_DepotProfileAndFeeInvoices') THEN
    CREATE UNIQUE INDEX "IX_platform_fee_invoices_owner_id_period_start" ON platform_fee_invoices (owner_id, period_start);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924131224_DepotProfileAndFeeInvoices') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924131224_DepotProfileAndFeeInvoices', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924133220_DepotBatchCodes') THEN
    CREATE SEQUENCE depot_batch_number START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924133220_DepotBatchCodes') THEN
    ALTER TABLE inventory_batches ADD code character varying(40);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924133220_DepotBatchCodes') THEN
    CREATE UNIQUE INDEX "IX_inventory_batches_code" ON inventory_batches (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924133220_DepotBatchCodes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924133220_DepotBatchCodes', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927154743_AddStaffAvatar') THEN
    ALTER TABLE users ADD avatar_url character varying(2048);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927154743_AddStaffAvatar') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260927154743_AddStaffAvatar', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929015907_DepotAdminInvoiceProof') THEN
    ALTER TABLE platform_invoices ADD payment_proof_url text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929015907_DepotAdminInvoiceProof') THEN
    ALTER TABLE platform_invoices ADD submitted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929015907_DepotAdminInvoiceProof') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929015907_DepotAdminInvoiceProof') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260929015907_DepotAdminInvoiceProof', '8.0.10');
    END IF;
END $EF$;
COMMIT;


