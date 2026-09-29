-- FILE DUY NHAT de tao lai 6 don demo Pickup Pool tai DA NANG.
-- pgAdmin: chon dung database ReTrack -> Query Tool -> mo file nay -> Execute (F5).
-- Hoac: psql -v ON_ERROR_STOP=1 -f db/seed_emp_pickup_pool_demo.sql
-- Dieu kien: da co schema, Employee/Seller va lien ket nhan vien-kho dang hoat dong.
-- Chi INSERT vao pickup_requests; khong DELETE/TRUNCATE va khong tao lai tai khoan.
-- Ket qua khi bang rong: 3 PENDING, 1 SCHEDULED, 2 DONE (1 hom nay UTC).
-- Chay lap lai khong nhan doi UUID va khong reset trang thai don da test.
-- Dia chi/toa do Da Nang da co san; khong can file chuyen dia diem rieng.
-- Location references and Google Maps links: docs/EMP_PICKUP_POOL_DEMO_DATA.md.
BEGIN;
DO $seed$
DECLARE
    employee_email text := 'employee@retrack.vn';
    seller_email text := 'seller@retrack.vn';
    employee_id uuid;
    seller_id uuid;
    depot_id uuid;
BEGIN
    SELECT u.id, s.depot_id INTO employee_id, depot_id
    FROM users u JOIN depot_staffs s ON s.user_id = u.id
    WHERE u.email = employee_email AND u.role = 'DEPOT_EMPLOYEE'
      AND u.is_active AND s.is_active AND s.staff_type = 'DEPOT_EMPLOYEE'
    ORDER BY s.id LIMIT 1;
    SELECT u.id INTO seller_id FROM users u
    WHERE u.email = seller_email AND u.role = 'SELLER' AND u.is_active;
    IF employee_id IS NULL OR depot_id IS NULL OR seller_id IS NULL THEN
        RAISE EXCEPTION 'Active demo employee, depot membership or seller is missing; no rows inserted.';
    END IF;

    INSERT INTO pickup_requests (
        id, seller_id, target_depot_id, accepted_collector_id,
        description, address, latitude, longitude, preferred_datetime,
        status, gross_amount, platform_fee_percentage, platform_fee_amount, net_amount,
        created_at, updated_at)
    SELECT v.id::uuid, seller_id, depot_id,
        CASE WHEN v.status = 'PENDING' THEN NULL ELSE employee_id END,
        '[DEMO-PICKUP-POOL-20260929] ' || v.description,
        v.address, v.latitude, v.longitude, v.preferred_datetime,
        v.status, 0, 1, 0, 0, CURRENT_TIMESTAMP - interval '2 days', v.updated_at
    FROM (VALUES
        ('e0000000-0929-4000-8000-000000000001',
         'Giấy carton và chai nhựa. Đơn giả lập để thử nhận đơn, không đến thu gom thực tế.',
         'Chợ Hàn, 119 Trần Phú, Đà Nẵng',
         16.0683600::numeric, 108.2244300::numeric,
         CURRENT_TIMESTAMP + interval '2 hours', 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000002',
         'Lon nhôm và giấy báo. Đơn giả lập để thử bản đồ và chỉ đường.',
         'Bảo tàng Điêu khắc Chăm, 2 đường 2 Tháng 9, Đà Nẵng',
         16.0602000::numeric, 108.2231700::numeric,
         CURRENT_TIMESTAMP + interval '4 hours', 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000003',
         'Sắt vụn. Đơn giả lập chưa hẹn giờ, phải nằm cuối danh sách đơn chờ.',
         'Chợ Cồn, 290 Hùng Vương, Đà Nẵng',
         16.0681700::numeric, 108.2145300::numeric,
         NULL::timestamptz, 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000004',
         'Đơn giả lập đã nhận để thử nút Xem chi tiết trên Dashboard. Chưa có cân hoặc thanh toán.',
         'Bảo tàng Điêu khắc Chăm, 2 đường 2 Tháng 9, Đà Nẵng',
         16.0602000::numeric, 108.2231700::numeric,
         CURRENT_TIMESTAMP + interval '1 hour', 'SCHEDULED', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000005',
         'Lịch sử giả lập hoàn tất hôm nay UTC, chỉ phục vụ thống kê Dashboard; không phải giao dịch tài chính.',
         'Chợ Hàn, 119 Trần Phú, Đà Nẵng',
         16.0683600::numeric, 108.2244300::numeric,
         NULL::timestamptz, 'DONE', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000006',
         'Lịch sử giả lập hoàn tất hôm qua UTC, chỉ phục vụ thống kê Dashboard; không phải giao dịch tài chính.',
         'Chợ Cồn, 290 Hùng Vương, Đà Nẵng',
         16.0681700::numeric, 108.2145300::numeric,
         NULL::timestamptz, 'DONE',
         (date_trunc('day', CURRENT_TIMESTAMP AT TIME ZONE 'UTC') - interval '12 hours') AT TIME ZONE 'UTC')
    ) AS v(id, description, address, latitude, longitude, preferred_datetime, status, updated_at)
    ON CONFLICT (id) DO NOTHING;
END
$seed$;
COMMIT;

SELECT id, status, address, latitude, longitude, preferred_datetime, updated_at
FROM pickup_requests WHERE description LIKE '[DEMO-PICKUP-POOL-20260929]%'
ORDER BY id;

