-- CHỈ DÙNG DB PHÁT TRIỂN: 3 lô PET (100/150/200 kg), 3 chuyến PENDING.
-- Chạy 01_driver_setup.sql trước, rồi chạy toàn bộ file này trong pgAdmin.
-- Tái sử dụng tài khoản/kho/nhà máy đang có; không sửa mật khẩu hay địa chỉ.
-- Tạo trực tiếp 3 lô tồn kho tại Depot; không tạo đơn Seller/Employee.
-- Không xóa/reset dữ liệu: chạy lại bỏ qua bộ đã seed, giữ trạng thái đã test.
BEGIN;
-- Keep this demo seed runnable against older development databases as well.
-- These flags are part of the current partnership contract and are only added
-- when missing; existing values are preserved.
ALTER TABLE factory_depot_partnerships
    ADD COLUMN IF NOT EXISTS blocked_by_depot BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS blocked_by_factory BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE inventory_batches
    ADD COLUMN IF NOT EXISTS code VARCHAR(40);
DO $seed$
DECLARE
    driver_email text := 'driver@retrack.vn';
    -- Để NULL: chọn nhà máy hoạt động có tọa độ và nhận PET, ưu tiên gần kho.
    chosen_factory_id uuid := NULL;
    v_driver uuid; v_depot uuid; v_factory uuid;
    v_address text; v_lat numeric; v_lng numeric;
    v_batch uuid; v_job uuid; v_notice uuid;
    v_kg numeric; i integer;
BEGIN
    IF to_regclass('notifications') IS NULL THEN
        RAISE EXCEPTION 'Chưa có schema ReTrack. Dựng schema trước khi seed.';
    END IF;
    SELECT u.id, d.id, d.address, d.latitude, d.longitude
    INTO v_driver, v_depot, v_address, v_lat, v_lng
    FROM users u JOIN depot_staffs s ON s.user_id = u.id
    JOIN depots d ON d.id = s.depot_id JOIN users owner_user ON owner_user.id = d.owner_id
    WHERE u.email = driver_email AND u.role = 'DRIVER' AND u.is_active
      AND s.staff_type = 'DRIVER' AND s.is_active
      AND owner_user.role = 'DEPOT_OWNER' AND owner_user.is_active
    ORDER BY s.id LIMIT 1;
    IF v_driver IS NULL THEN RAISE EXCEPTION 'Thiếu Driver hoạt động hoặc liên kết kho: %', driver_email; END IF;
    IF v_lat IS NULL OR v_lng IS NULL OR v_lat NOT BETWEEN -90 AND 90 OR v_lng NOT BETWEEN -180 AND 180 THEN
        RAISE EXCEPTION 'Kho của Driver chưa có tọa độ hợp lệ để test bản đồ.';
    END IF;
    SELECT f.id INTO v_factory FROM factories f JOIN users u ON u.id = f.owner_id
    WHERE u.is_active AND u.role = 'FACTORY'
      AND f.latitude BETWEEN -90 AND 90 AND f.longitude BETWEEN -180 AND 180
      AND (chosen_factory_id IS NULL OR f.id = chosen_factory_id)
      AND (f.capacity_kg_per_month = 0 OR f.capacity_kg_per_month >= 450)
      AND 'PET' = ANY(string_to_array(replace(upper(f.accepted_materials), ' ', ''), ','))
      AND NOT EXISTS (SELECT 1 FROM factory_depot_partnerships p WHERE p.depot_id = v_depot AND p.factory_id = f.id
          AND (p.status = 'BLOCKED' OR p.blocked_by_depot OR p.blocked_by_factory))
    ORDER BY power(f.latitude - v_lat, 2) + power(f.longitude - v_lng, 2), f.id LIMIT 1;
    IF v_factory IS NULL THEN
        RAISE EXCEPTION 'Cần Factory hoạt động có tọa độ, nhận PET, không bị chặn; sức chứa chưa giới hạn hoặc >=450kg.';
    END IF;
    -- Cùng khóa depot với nghiệp vụ tạo/phân bổ lô; tránh seed đồng thời.
    PERFORM id FROM depots WHERE id = v_depot FOR UPDATE;
    FOR i IN 1..3 LOOP
        v_batch := ('d6100000-0000-4000-8000-' || lpad(i::text, 12, '0'))::uuid;
        v_job := ('d6200000-0000-4000-8000-' || lpad(i::text, 12, '0'))::uuid;
        v_notice := ('d6500000-0000-4000-8000-' || lpad(i::text, 12, '0'))::uuid;
        v_kg := 50 + i * 50;
        IF EXISTS (SELECT 1 FROM inventory_batches WHERE id = v_batch) THEN
            IF NOT EXISTS (SELECT 1 FROM inventory_batches WHERE id = v_batch AND depot_id = v_depot
                AND description LIKE '[DEMO-DRIVER-POOL]%' AND code = 'DRV-DEMO-0' || i)
                OR NOT EXISTS (SELECT 1 FROM transport_jobs WHERE id = v_job AND batch_id = v_batch) THEN
                RAISE EXCEPTION 'Bộ demo % đã tồn tại nhưng không khớp hoặc thiếu chuyến. Không tự ghi đè.', i;
            END IF;
            CONTINUE;
        END IF;
        -- INSERT có chủ đích không ON CONFLICT: bộ dữ liệu dở dang phải rollback,
        -- không tái tạo lô trên nguồn hàng/chuyến cũ đã được sử dụng.
        INSERT INTO inventory_batches(id, code, depot_id, target_factory_id, material_type, declared_weight_kg, description, status,
            created_at, updated_at)
        VALUES(v_batch, 'DRV-DEMO-0' || i, v_depot, v_factory, 'PET', v_kg,
            '[DEMO-DRIVER-POOL] Lô giả lập ' || i || ' để test nhận chuyến và bản đồ. Không giao hàng thực tế.', 'ACCEPTED',
            CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        INSERT INTO transport_jobs(id, batch_id, status, created_at, updated_at)
        VALUES(v_job, v_batch, 'PENDING', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
        INSERT INTO notifications(id, user_id, title, message, is_read, created_at, transport_job_id)
        VALUES(v_notice, v_driver, 'Chuyến vận chuyển demo mới', 'Lô DRV-DEMO-0' || i || ' đang chờ nhận. Chỉ dùng kiểm thử.',
            FALSE, CURRENT_TIMESTAMP, v_job);
    END LOOP;
END
$seed$;
COMMIT;

SELECT b.code, b.declared_weight_kg, b.status AS batch_status, j.status AS job_status, j.driver_id,
       d.name AS depot, d.address AS depot_address, d.latitude AS depot_lat, d.longitude AS depot_lng,
       f.name AS factory, f.address AS factory_address, f.latitude AS factory_lat, f.longitude AS factory_lng
FROM inventory_batches b JOIN transport_jobs j ON j.batch_id = b.id
JOIN depots d ON d.id = b.depot_id JOIN factories f ON f.id = b.target_factory_id
WHERE b.id IN ('d6100000-0000-4000-8000-000000000001', 'd6100000-0000-4000-8000-000000000002', 'd6100000-0000-4000-8000-000000000003')
ORDER BY b.code;
