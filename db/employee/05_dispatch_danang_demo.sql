-- Sau 04_employee_dispatch.sql. Chạy toàn bộ trên DB test cá nhân; không cần xóa bảng.
-- 36 đơn: 24 cần thu gom sớm + 6 hẹn trong ca + 6 hẹn ngày kia.
-- 6 nhân viên, 6 Seller demo, 6 khu vực, 1 kho demo riêng của Owner hiện có.
-- Tài khoản: employee.dispatch01@retrack.test ... employee.dispatch06@retrack.test.
-- Sao chép HASH từ employee@retrack.vn khi tạo lần đầu; dùng mật khẩu hiện tại của tài khoản nguồn.
-- Không thay password/tài khoản nguồn, không tạo thanh toán/tồn kho.
-- Địa điểm công cộng/Plus Code từ bộ seed Đà Nẵng hiện có; nhiều đơn cùng điểm là dữ liệu mô phỏng.
-- Không phải yêu cầu thu gom thực tế của các địa điểm/tổ chức này.
-- Tọa độ giữ nguyên, KHÔNG cộng độ lệch ngẫu nhiên làm ghim rơi ngoài đường.
BEGIN;
DO $seed$
<<fixture>>
DECLARE
    owner_id uuid;
    employee_source users%ROWTYPE;
    seller_source users%ROWTYPE;
    depot_id uuid := 'ed200000-0000-4000-8000-000000000001';
    area_id uuid; employee_id uuid; seller_id uuid; pickup_id uuid; shift_id uuid;
    point record; i integer; j integer;
    shift_start timestamptz := CURRENT_TIMESTAMP;
    shift_end timestamptz := CURRENT_TIMESTAMP + interval '20 hours';
BEGIN
    SELECT * INTO employee_source FROM users WHERE email = 'employee@retrack.vn' AND role = 'DEPOT_EMPLOYEE' AND is_active;
    SELECT * INTO seller_source FROM users WHERE email = 'seller@retrack.vn' AND role = 'SELLER' AND is_active;
    SELECT d.owner_id INTO owner_id FROM depots d JOIN users u ON u.id = d.owner_id
        WHERE u.is_active AND u.role = 'DEPOT_OWNER' ORDER BY d.id LIMIT 1;
    IF employee_source.id IS NULL OR seller_source.id IS NULL OR owner_id IS NULL THEN
        RAISE EXCEPTION 'Cần employee@retrack.vn, seller@retrack.vn và Owner đang hoạt động. Chưa seed gì.';
    END IF;
    INSERT INTO depots(id, owner_id, name, address, latitude, longitude, rating, verification_status, created_at)
    VALUES(depot_id, owner_id, '[DEMO-DISPATCH] Kho điều phối Đà Nẵng',
        'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng — kho mô phỏng', 15.9874125, 108.2562344, 0, 'APPROVED', CURRENT_TIMESTAMP)
    ON CONFLICT(id) DO NOTHING;
    i := 0;
    FOR point IN SELECT * FROM (VALUES
        ('Ngũ Hành Sơn', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125,108.2562344),
        ('Hải Châu — Chợ Hàn', 'Chợ Hàn, 119 Trần Phú, Đà Nẵng',16.0683600,108.2244300),
        ('Hải Châu — Bảo tàng Chăm', 'Bảo tàng Điêu khắc Chăm, 2 đường 2 Tháng 9, Đà Nẵng',16.0602000,108.2231700),
        ('Thanh Khê — Chợ Cồn', 'Chợ Cồn, 290 Hùng Vương, Đà Nẵng',16.0681700,108.2145300),
        ('Hải Châu — Trung tâm Hành chính', 'Trung tâm Hành chính thành phố Đà Nẵng, 24 Trần Phú, Đà Nẵng',16.0770864,108.2231701),
        ('Liên Chiểu — Đại học Bách khoa', 'Trung tâm Học liệu và Truyền thông, 54 Nguyễn Lương Bằng, Đà Nẵng',16.0746638,108.1506880)
    ) AS v(name,address,lat,lng)
    LOOP
        i := i + 1;
        area_id := ('ed210000-0000-4000-8000-' || lpad(i::text,12,'0'))::uuid;
        employee_id := ('ed220000-0000-4000-8000-' || lpad(i::text,12,'0'))::uuid;
        seller_id := ('ed230000-0000-4000-8000-' || lpad(i::text,12,'0'))::uuid;
        -- UUID cố định/unique email: xung đột với dữ liệu ngoài fixture làm transaction dừng.
        INSERT INTO users(id,email,password_hash,role,full_name,phone,is_active,created_at,updated_at)
        VALUES(employee_id,'employee.dispatch' || lpad(i::text,2,'0') || '@retrack.test',employee_source.password_hash,
            'DEPOT_EMPLOYEE','Nhân viên điều phối demo ' || i,'090990000' || i,true,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)
        ON CONFLICT(id) DO NOTHING;
        INSERT INTO users(id,email,password_hash,role,full_name,phone,is_active,created_at,updated_at)
        VALUES(seller_id,'seller.dispatch' || lpad(i::text,2,'0') || '@retrack.test',seller_source.password_hash,
            'SELLER','Người bán mô phỏng khu vực ' || i,'090980000' || i,true,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)
        ON CONFLICT(id) DO NOTHING;
        INSERT INTO depot_staffs(id,depot_id,user_id,staff_type,is_active)
        VALUES(('ed240000-0000-4000-8000-' || lpad(i::text,12,'0'))::uuid,depot_id,employee_id,'DEPOT_EMPLOYEE',true)
        ON CONFLICT(id) DO NOTHING;
        INSERT INTO employee_collection_areas(id,depot_id,name,latitude,longitude,radius_meters,is_active)
        VALUES(area_id,depot_id,point.name,point.lat,point.lng,2500,true) ON CONFLICT(id) DO NOTHING;
        -- Chạy lại trong ca không bật lại người đã STOPPED/FULL. Ngày khác chỉ thêm ca khi ca cũ đã hết.
        IF NOT EXISTS(SELECT 1 FROM employee_dispatch_shifts WHERE employee_dispatch_shifts.employee_id = fixture.employee_id
            AND ends_at > shift_start) THEN
            shift_id := gen_random_uuid();
            INSERT INTO employee_dispatch_shifts(id,area_id,employee_id,starts_at,ends_at,availability,vehicle,
                capacity_kg,max_pending_orders,updated_at)
            VALUES(shift_id,area_id,employee_id,shift_start,shift_end,'AVAILABLE',
                CASE WHEN i % 2 = 0 THEN 'car' ELSE 'motorcycle' END,
                CASE WHEN i % 2 = 0 THEN 500 ELSE 100 END,30,CURRENT_TIMESTAMP);
        END IF;
        FOR j IN 1..6 LOOP
            pickup_id := ('ed250000-0000-4000-8000-' || lpad(((i-1)*6+j)::text,12,'0'))::uuid;
            INSERT INTO pickup_requests(id,seller_id,target_depot_id,description,address,latitude,longitude,preferred_datetime,
                status,gross_amount,platform_fee_percentage,platform_fee_amount,net_amount,created_at,updated_at)
            VALUES(pickup_id,seller_id,depot_id,
                '[DEMO-DISPATCH] Khu ' || i || ' / đơn ' || j || '. Chai nhựa, giấy carton; chưa cân. Không thu gom thực tế.',
                point.address,point.lat,point.lng,
                CASE WHEN j <= 4 THEN NULL WHEN j = 5 THEN CURRENT_TIMESTAMP + interval '1 hour'
                     ELSE CURRENT_TIMESTAMP + interval '2 days' END,
                'PENDING',0,5,0,0,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)
            ON CONFLICT(id) DO NOTHING;
        END LOOP;
    END LOOP;
    RAISE NOTICE 'Đã bổ sung fixture 36 đơn / 6 nhân viên. Backend tự phân công trong khoảng 15 giây; đơn ngày kia chưa được giao.';
END $seed$;
COMMIT;

-- Đối chiếu kết quả; chỉ fixture, không đọc/sửa các đơn khác.
SELECT u.email,p.status,count(*) AS orders
FROM pickup_requests p LEFT JOIN users u ON u.id=p.accepted_collector_id
WHERE p.id::text LIKE 'ed250000-%'
GROUP BY u.email,p.status ORDER BY u.email,p.status;
-- Mở Google Maps đúng tọa độ đã seed để đối chiếu ghim:
SELECT name, 'https://www.google.com/maps/search/?api=1&query=' || latitude || ',' || longitude AS maps_url
FROM employee_collection_areas WHERE depot_id='ed200000-0000-4000-8000-000000000001';
