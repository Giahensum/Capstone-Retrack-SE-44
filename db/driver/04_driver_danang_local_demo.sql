-- CHỈ CHẠY TRÊN DATABASE PHÁT TRIỂN CÁ NHÂN.
-- Chạy 01_driver_setup.sql và 03_driver_delivery.sql trước.
-- 3 lô mới, kho/nhà máy giả lập cùng điểm test Employee X7P4+XF9.
-- Không đổi địa chỉ cũ, không tạo đơn pickup, không reset chuyến đã test.
BEGIN;
-- Database cũ có thể chưa bổ sung cột ảnh lô. Giữ đúng kiểu/default của schema chung.
-- Đặt trong cùng transaction: nếu seed lỗi thì cả cập nhật này cũng rollback.
ALTER TABLE inventory_batches
    ADD COLUMN IF NOT EXISTS image_urls TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[];
DO $seed$
DECLARE
    driver_email text := 'driver@retrack.vn';
    factory_demo_email text := 'factory.driver-local@retrack.test';
    v_lat numeric := 15.9874125;
    v_lng numeric := 108.256234375;
    v_address text := 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng';
    v_driver uuid; v_owner uuid; v_factory_source uuid;
    v_depot uuid := 'd8100000-0000-4000-8000-000000000001';
    v_factory uuid := 'd8200000-0000-4000-8000-000000000001';
    v_factory_user uuid := 'd8300000-0000-4000-8000-000000000001';
    v_staff uuid := 'd8400000-0000-4000-8000-000000000001';
    v_batch uuid; v_job uuid; v_notice uuid; i integer;
BEGIN
    IF to_regclass('driver_delivery_events') IS NULL THEN
        RAISE EXCEPTION 'Chạy db/driver/03_driver_delivery.sql trước khi seed test check-in.';
    END IF;
    -- Tuần tự hóa các lần chạy cùng fixture, không reset dữ liệu đã tồn tại.
    PERFORM pg_advisory_xact_lock(20261008, 64067);
    SELECT u.id, d.owner_id INTO v_driver, v_owner
    FROM users u JOIN depot_staffs s ON s.user_id = u.id
    JOIN depots d ON d.id = s.depot_id JOIN users owner_user ON owner_user.id = d.owner_id
    WHERE u.email = driver_email AND u.role = 'DRIVER' AND u.is_active
      AND s.staff_type = 'DRIVER' AND s.is_active
      AND owner_user.role = 'DEPOT_OWNER' AND owner_user.is_active
    ORDER BY s.id LIMIT 1;
    IF v_driver IS NULL THEN
        RAISE EXCEPTION 'Thiếu tài xế % và liên kết kho đang hoạt động. Sửa driver_email ở đầu file nếu dùng tài khoản khác.', driver_email;
    END IF;

    -- Đọc lại tọa độ đơn test tại chỗ nếu đã chạy seed Employee; không ghi vào pickup.
    SELECT COALESCE(p.latitude, v_lat), COALESCE(p.longitude, v_lng), COALESCE(p.address, v_address)
    INTO v_lat, v_lng, v_address FROM pickup_requests p
    WHERE p.id = 'e0000000-0929-4000-8000-000000000013';
    IF NOT FOUND THEN
        v_lat := 15.9874125; v_lng := 108.256234375;
        v_address := 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng';
    END IF;
    IF v_lat NOT BETWEEN -90 AND 90 OR v_lng NOT BETWEEN -180 AND 180 THEN
        RAISE EXCEPTION 'Tọa độ test Employee không hợp lệ.';
    END IF;

    -- Tài khoản Factory demo riêng để không làm dịch vụ Factory chọn nhầm giữa hai nhà máy.
    -- Sao chép hash đã có trong DB, không nhúng/in mật khẩu hoặc hash vào file/log.
    IF NOT EXISTS (SELECT 1 FROM users WHERE id = v_factory_user) THEN
        SELECT id INTO v_factory_source FROM users
        WHERE role = 'FACTORY' AND is_active AND password_hash <> ''
        ORDER BY (email = 'factory@retrack.vn') DESC, id LIMIT 1;
        IF v_factory_source IS NULL THEN RAISE EXCEPTION 'Cần một tài khoản Factory hoạt động để tạo tài khoản demo.'; END IF;
        INSERT INTO users(id, email, password_hash, role, full_name, phone, is_active, created_at, updated_at)
        SELECT v_factory_user, factory_demo_email, password_hash, 'FACTORY',
            '[DEMO-DRIVER-LOCAL] Nhà máy test Đà Nẵng', '', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
        FROM users WHERE id = v_factory_source;
        RAISE NOTICE 'Factory demo: %, dùng mật khẩu hiện tại của tài khoản nguồn % (chỉ sao chép khi tạo lần đầu).',
            factory_demo_email, (SELECT email FROM users WHERE id = v_factory_source);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM users WHERE id = v_factory_user AND email = factory_demo_email
        AND role = 'FACTORY' AND is_active AND full_name LIKE '[DEMO-DRIVER-LOCAL]%') THEN
        RAISE EXCEPTION 'UUID tài khoản demo đã tồn tại nhưng không khớp hoặc bị vô hiệu hóa; không ghi đè.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM depots WHERE id = v_depot) THEN
        INSERT INTO depots(id, owner_id, name, address, latitude, longitude, rating, created_at)
        VALUES(v_depot, v_owner, '[DEMO-DRIVER-LOCAL] Kho test tại chỗ Đà Nẵng',
            v_address, v_lat, v_lng, 0, CURRENT_TIMESTAMP);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM depots WHERE id = v_depot AND owner_id = v_owner
        AND name LIKE '[DEMO-DRIVER-LOCAL]%' AND address = v_address
        AND abs(latitude - v_lat) < 0.000001 AND abs(longitude - v_lng) < 0.000001) THEN
        RAISE EXCEPTION 'Kho fixture không khớp chủ kho/tọa độ; không tự đổi địa điểm hoặc dữ liệu cũ.';
    END IF;
    PERFORM id FROM depots WHERE id = v_depot FOR UPDATE;
    IF NOT EXISTS (SELECT 1 FROM depot_staffs WHERE id = v_staff) THEN
        INSERT INTO depot_staffs(id, depot_id, user_id, staff_type, is_active)
        VALUES(v_staff, v_depot, v_driver, 'DRIVER', TRUE);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM depot_staffs WHERE id = v_staff AND depot_id = v_depot
        AND user_id = v_driver AND staff_type = 'DRIVER' AND is_active) THEN
        RAISE EXCEPTION 'Liên kết tài xế fixture không khớp hoặc không hoạt động; không ghi đè.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM factories WHERE id = v_factory) THEN
        INSERT INTO factories(id, owner_id, name, address, latitude, longitude, rating,
            capacity_kg_per_month, minimum_purity_percent, accepted_materials, created_at)
        VALUES(v_factory, v_factory_user, '[DEMO-DRIVER-LOCAL] Nhà máy test tại chỗ Đà Nẵng',
            v_address, v_lat, v_lng, 0, 10000, 0, 'PET', CURRENT_TIMESTAMP);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM factories WHERE id = v_factory AND owner_id = v_factory_user
        AND name LIKE '[DEMO-DRIVER-LOCAL]%' AND address = v_address
        AND abs(latitude - v_lat) < 0.000001 AND abs(longitude - v_lng) < 0.000001) THEN
        RAISE EXCEPTION 'Nhà máy fixture không khớp chủ/tọa độ; không tự đổi dữ liệu cũ.';
    END IF;

    FOR i IN 1..3 LOOP
        v_batch := ('d8500000-0000-4000-8000-' || lpad(i::text, 12, '0'))::uuid;
        v_job := ('d8600000-0000-4000-8000-' || lpad(i::text, 12, '0'))::uuid;
        v_notice := ('d8700000-0000-4000-8000-' || lpad(i::text, 12, '0'))::uuid;
        IF EXISTS (SELECT 1 FROM inventory_batches WHERE id = v_batch) THEN
            IF NOT EXISTS (SELECT 1 FROM inventory_batches WHERE id = v_batch AND depot_id = v_depot
                AND target_factory_id = v_factory AND code = 'DRV-DN-LOCAL-0' || i
                AND description LIKE '[DEMO-DRIVER-LOCAL]%')
                OR NOT EXISTS (SELECT 1 FROM transport_jobs WHERE id = v_job AND batch_id = v_batch) THEN
                RAISE EXCEPTION 'Lô demo % tồn tại nhưng thiếu chuyến hoặc không khớp; không ghi đè.', i;
            END IF;
            CONTINUE;
        END IF;
        INSERT INTO inventory_batches(id, code, depot_id, target_factory_id, material_type,
            declared_weight_kg, description, status, image_urls, created_at, updated_at)
        VALUES(v_batch, 'DRV-DN-LOCAL-0' || i, v_depot, v_factory, 'PET', 50 + i * 50,
            '[DEMO-DRIVER-LOCAL] Test camera/GPS tại chỗ. Kho và nhà máy giả lập cùng vị trí người test. Không giao hàng hay thanh toán thực tế.',
            'TRANSPORT_READY', ARRAY[]::TEXT[], CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        INSERT INTO transport_jobs(id, batch_id, status, created_at, updated_at)
        VALUES(v_job, v_batch, 'PENDING', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        INSERT INTO notifications(id, user_id, title, message, is_read, created_at, transport_job_id)
        VALUES(v_notice, v_driver, 'Chuyến demo tại chỗ Đà Nẵng',
            'DRV-DN-LOCAL-0' || i || ': thử nhận chuyến, camera/GPS lấy hàng, khởi hành và giao hàng tại chỗ.',
            FALSE, CURRENT_TIMESTAMP, v_job);
    END LOOP;
    RAISE NOTICE 'Đã chuẩn bị 3 lô test tại chỗ. Không cần xóa 3 lô cũ; chuyến đã test không bị reset.';
END
$seed$;
COMMIT;

SELECT b.code, b.declared_weight_kg, j.status, j.driver_id,
    d.name AS depot, d.address AS depot_address, d.latitude AS depot_lat, d.longitude AS depot_lng,
    f.name AS factory, f.address AS factory_address, f.latitude AS factory_lat, f.longitude AS factory_lng
FROM inventory_batches b JOIN transport_jobs j ON j.batch_id = b.id
JOIN depots d ON d.id = b.depot_id JOIN factories f ON f.id = b.target_factory_id
WHERE b.id IN ('d8500000-0000-4000-8000-000000000001', 'd8500000-0000-4000-8000-000000000002', 'd8500000-0000-4000-8000-000000000003')
ORDER BY b.code;
