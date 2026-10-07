-- CHẠY MỘT LẦN TOÀN BỘ FILE để tạo lại đủ 13 đơn demo tại ĐÀ NẴNG.
-- Dùng được khi bảng pickup_requests đã bị xóa hết bản ghi.
-- pgAdmin: chọn đúng database ReTrack -> Query Tool -> mở file này -> F5.
-- Không bôi đen riêng một đoạn INSERT/VALUES; cần chạy từ BEGIN đến hết file.
-- File gồm đầy đủ:
--   NHÓM A: 3 đơn chờ CŨ: Chợ Hàn, Bảo tàng Điêu khắc Chăm, Chợ Cồn.
--   NHÓM B: 3 đơn thống kê: 1 SCHEDULED và 2 DONE.
--   NHÓM C: 6 đơn chờ MỚI để test nhiều tuyến đường.
--   NHÓM D: 1 đơn chờ tại vị trí người test cung cấp để test check-in + cân tại chỗ.
-- Kết quả trên bảng rỗng: 13 bản ghi = 10 PENDING + 1 SCHEDULED + 2 DONE.
-- App Đơn chờ hiển thị 10 đơn; 3 đơn SCHEDULED/DONE không thuộc danh sách này.
-- Hoac: psql -v ON_ERROR_STOP=1 -f db/employee/02_employee_demo.sql
-- Dieu kien: da co schema, Employee/Seller va lien ket nhan vien-kho dang hoat dong.
-- Mặc định chỉ INSERT; bật reset_employee_demo=true để reset riêng đơn demo, không xóa tài khoản.
-- Ket qua khi chua co UUID demo: 10 PENDING, 1 SCHEDULED, 2 DONE (1 hom nay UTC).
-- Nếu bảng đã có một phần dữ liệu demo, file bổ sung các UUID còn thiếu.
-- Chay lap lai khong nhan doi UUID va khong reset trang thai don da test.
-- Dia chi/toa do Da Nang da co san; khong can file chuyen dia diem rieng.
-- Huong dan app: mobile/ROUTING_TEST_GUIDE.md.
--
-- BO SUNG 2026-10-01: dia chi/toa do lay tu Goong Geocode; da goi Direction
-- voi alternatives=true cho ca car va bike tu mot diem xuat phat tai Ngu Hanh Son.
-- Ca 6 diem moi deu tra ve 2 tuyen hop le cho moi phuong tien trong lan kiem tra.
-- Nhóm C không lưu điểm xuất phát người test; app vẫn dùng GPS điện thoại.
--
-- NEN TEST TRUOC: [TUYEN-01] Ba Na Hills, chon O TO:
--   Lan kiem tra: tuyen A = 30.540 m / 2.903 giay; B = 29.790 m / 2.947 giay.
--   Nhanh nhat du kien -> A (khoang 49 phut).
--   It km nhat va Can bang -> B (khoang 50 phut), vi 2.947 <= 2.903 * 1,15.
-- Day la ket qua tai diem xuat phat/thoi diem kiem tra, KHONG phai so lieu co dinh
-- cho moi GPS. Goong co the thay doi so tuyen, quang duong va thoi gian tra ve.
-- Nhieu tuyen khong bat buoc 3 tieu chi cho 3 ket qua khac nhau: mot tuyen co the
-- vua ngan nhat vua nhanh nhat. Du lieu km/phut khong duoc seed vao database.
-- Các đơn đều GIẢ LẬP, không phải yêu cầu thu gom của người bán thực tế.
-- Nhóm D dùng đúng Plus Code người test yêu cầu: X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng.
-- Khôi phục mã đầy đủ theo khu vực Đà Nẵng: 7P7CX7P4+XF9.
-- Tọa độ tâm ô Plus Code: 15.9874125, 108.256234375 (làm tròn theo cột DB).
-- Nguồn thuật toán: https://github.com/google/open-location-code
-- Đây là điểm giải mã Plus Code, không phải kết quả geocode tên đường/số nhà.
-- Chọn đơn [CHECKIN-LOCAL-01] -> nhận đơn -> check-in -> phân loại/cân/định giá.
-- Vẫn áp dụng GPS thật: cách điểm tối đa 200 m, sai số <= 50 m; không bỏ validation.
-- Không cần xóa bảng để thêm đơn mới: chạy lại file sẽ chỉ bổ sung UUID thiếu.
-- Nếu đã xóa pickup_requests, chạy cả file sẽ khôi phục đủ 13 đơn.
-- Trước khi test UC-51/52/53, chạy db/employee/01_employee_setup.sql nếu chưa bổ sung bảng check-in.
BEGIN;
-- false: chỉ bổ sung UUID thiếu; true: xóa riêng 13 đơn demo rồi tạo lại trong cùng transaction.
-- Chỉ bật true trên database test cá nhân; dừng backend trước khi reset.
SET LOCAL retrack.reset_employee_demo = 'false';
DO $reset$
BEGIN
IF current_setting('retrack.reset_employee_demo') = 'true' THEN
CREATE TEMP TABLE reset_employee_demo_ids ON COMMIT DROP AS
SELECT id FROM pickup_requests
WHERE id BETWEEN 'e0000000-0929-4000-8000-000000000001'::uuid
             AND 'e0000000-0929-4000-8000-000000000013'::uuid
  AND description LIKE '[DEMO-PICKUP-POOL-20260929]%';

-- Không tự xóa lịch sử tài chính hoặc nguồn tồn kho đã phát sinh.
    IF EXISTS (
        SELECT 1 FROM pickup_requests p JOIN reset_employee_demo_ids d ON d.id = p.id
        WHERE p.payment_proof_url IS NOT NULL OR p.status = 'PAYMENT_SENT'
           OR (p.status = 'DONE' AND (p.gross_amount <> 0 OR EXISTS (
               SELECT 1 FROM pickup_request_items i WHERE i.pickup_request_id = p.id)))
    ) OR EXISTS (
        SELECT 1 FROM platform_transactions t JOIN reset_employee_demo_ids d ON d.id = t.source_id
        WHERE t.source_type = 'PICKUP_REQUEST'
    ) THEN
        RAISE EXCEPTION 'Có đơn demo đã phát sinh thanh toán/tồn kho. Không reset các đơn này bằng script này.';
    END IF;


DELETE FROM seller_depot_reviews WHERE pickup_request_id IN (SELECT id FROM reset_employee_demo_ids);
-- Chỉ xóa thông báo có UUID rõ ràng của các đơn demo; giữ thông báo không xác định được đơn.
DELETE FROM notifications n WHERE EXISTS (
    SELECT 1 FROM reset_employee_demo_ids d WHERE strpos(COALESCE(n.message, ''), d.id::text) > 0
);
-- Các bảng con tự xóa theo FK ON DELETE CASCADE:
-- pickup_request_items, pickup_checkins, employee_collection_events (nếu đã tạo).
DELETE FROM pickup_requests WHERE id IN (SELECT id FROM reset_employee_demo_ids);


END IF;
END
$reset$;

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
        v.status, 0, 5, 0, 0, CURRENT_TIMESTAMP - interval '2 days', v.updated_at
    FROM (VALUES
        -- NHÓM A — 3 ĐƠN CHỜ CŨ (giữ nguyên địa chỉ và UUID).
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
        -- NHÓM B — 1 ĐƠN ĐÃ NHẬN + 2 ĐƠN HOÀN TẤT để test Dashboard.
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
         (date_trunc('day', CURRENT_TIMESTAMP AT TIME ZONE 'UTC') - interval '12 hours') AT TIME ZONE 'UTC'),
        -- NHÓM C — 6 ĐƠN CHỜ MỚI để test tuyến đường ô tô/xe máy.
        ('e0000000-0929-4000-8000-000000000007',
         '[TUYEN-01] Bà Nà Hills - ưu tiên test ô tô: đã ghi nhận tuyến ít km hơn nhưng chậm hơn tuyến nhanh nhất; cân bằng chọn tuyến ngắn trong ngưỡng 15%. Đơn giả lập, không đến thu gom thực tế.',
         'Khu du lịch Bà Nà Hills, thôn An Sơn, Đà Nẵng',
         16.0259108::numeric, 108.0322546::numeric,
         CURRENT_TIMESTAMP + interval '30 minutes', 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000008',
         '[TUYEN-02] Đại học Bách khoa - test nhiều tuyến ô tô/xe máy về phía tây bắc; cùng quãng đường có thể khác thời gian theo phương tiện. Đơn giả lập, không đến thu gom thực tế.',
         'Trung tâm Học liệu và Truyền thông - Đại học Bách khoa, 54 Nguyễn Lương Bằng, Đà Nẵng',
         16.0746638::numeric, 108.1506880::numeric,
         CURRENT_TIMESTAMP + interval '45 minutes', 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000009',
         '[TUYEN-03] Trung tâm Hành chính - test nhiều phương án đến khu vực phía bắc sông Hàn. Các tiêu chí có thể cùng chọn một tuyến. Đơn giả lập, không đến thu gom thực tế.',
         'Trung tâm Hành chính thành phố Đà Nẵng, 24 Trần Phú, Đà Nẵng',
         16.0770864::numeric, 108.2231701::numeric,
         CURRENT_TIMESTAMP + interval '60 minutes', 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000010',
         '[TUYEN-04] Bệnh viện Đà Nẵng - test nhiều tuyến nội thành và thay đổi thời gian ước tính khi đổi phương tiện. Đơn giả lập, không đến thu gom thực tế.',
         'Bệnh viện Đà Nẵng, 124 Hải Phòng, Đà Nẵng',
         16.0721056::numeric, 108.2156940::numeric,
         CURRENT_TIMESTAMP + interval '75 minutes', 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000011',
         '[TUYEN-05] Chợ Túy Loan - test nhiều tuyến về phía tây; so sánh số km và thời gian ô tô/xe máy. Đơn giả lập, không đến thu gom thực tế.',
         'Chợ Túy Loan, Đà Nẵng',
         15.9903511::numeric, 108.1409952::numeric,
         CURRENT_TIMESTAMP + interval '90 minutes', 'PENDING', CURRENT_TIMESTAMP),
        ('e0000000-0929-4000-8000-000000000012',
         '[TUYEN-06] Đại học Đông Á - test nhiều tuyến ngắn hơn và thay đổi hình tuyến theo phương tiện. Đơn giả lập, không đến thu gom thực tế.',
         'Đại học Đông Á, 33 Xô Viết Nghệ Tĩnh, Đà Nẵng',
         16.0321559::numeric, 108.2213119::numeric,
         CURRENT_TIMESTAMP + interval '105 minutes', 'PENDING', CURRENT_TIMESTAMP),
        -- NHÓM D — ĐƠN TẠI CHỖ, chưa nhận/chưa check-in/chưa có kết quả cân.
        ('e0000000-0929-4000-8000-000000000013',
         '[CHECKIN-LOCAL-01] Test tại chỗ: nhận đơn, chụp ảnh camera + GPS, phân loại, cân và định giá. Đơn giả lập do người test tạo, không phát sinh giao dịch thực tế.',
         'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng',
         15.9874125::numeric, 108.256234375::numeric,
         CURRENT_TIMESTAMP + interval '5 minutes', 'PENDING', CURRENT_TIMESTAMP)
    ) AS v(id, description, address, latitude, longitude, preferred_datetime, status, updated_at)
    ON CONFLICT (id) DO NOTHING;
END
$seed$;
COMMIT;

-- Kết quả kiểm tra sau khi chạy: trên bảng vừa xóa phải là 13 / 10 / 1 / 2.
SELECT COUNT(*) AS tong_don_demo,
       COUNT(*) FILTER (WHERE status = 'PENDING') AS don_cho,
       COUNT(*) FILTER (WHERE status = 'SCHEDULED') AS don_da_nhan,
       COUNT(*) FILTER (WHERE status = 'DONE') AS don_hoan_tat
FROM pickup_requests
WHERE description LIKE '[DEMO-PICKUP-POOL-20260929]%';

-- Danh sách đầy đủ: 3 đơn cũ là UUID ...001, ...002, ...003.
SELECT id, status, address, latitude, longitude, preferred_datetime, updated_at
FROM pickup_requests WHERE description LIKE '[DEMO-PICKUP-POOL-20260929]%'
ORDER BY id;

