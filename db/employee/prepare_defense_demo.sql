-- Chuẩn bị bộ 160 đơn điều phối trên DB demo ĐÃ CÓ bootstrap mới.
-- Không phải bootstrap thứ hai. Không drop/truncate, không tạo lại tài khoản/đơn đã dùng.
-- Cần bộ 100 đơn gốc; bổ sung 60 đơn mới còn thiếu (101–160), không đổi ID gốc.
-- Dừng API, backup và chọn đúng DB trước khi chạy. Chạy trong pgAdmin hoặc psql.
-- Nếu Query Tool báo lỗi, chạy ROLLBACK; trước khi sửa/chạy lại.
BEGIN;
-- CHỈ ĐỔI NGÀY Ở DÒNG NÀY nếu test ngày khác (YYYY-MM-DD, giờ nghiệp vụ Việt Nam).
SET LOCAL retrack.demo_date = '2026-12-07';
SELECT pg_advisory_xact_lock(62051001);

-- Nâng bổ sung lịch khi DB đã có bộ seed mới; nếu preflight lỗi toàn bộ rollback.
ALTER TABLE pickup_requests ADD COLUMN IF NOT EXISTS collection_date date;
ALTER TABLE pickup_requests ADD COLUMN IF NOT EXISTS preferred_end_datetime timestamptz;
ALTER TABLE pickup_requests ADD COLUMN IF NOT EXISTS requires_owner_review boolean NOT NULL DEFAULT false;
ALTER TABLE pickup_requests ADD COLUMN IF NOT EXISTS dispatch_review_reason text;
CREATE INDEX IF NOT EXISTS ix_pickup_dispatch_date ON pickup_requests(collection_date,status) WHERE requires_owner_review=false;

CREATE TEMP TABLE defense_employees(email text PRIMARY KEY,full_name text NOT NULL,depot_id uuid NOT NULL) ON COMMIT DROP;
INSERT INTO defense_employees VALUES
('employee.dispatch01@retrack.test','Lê Hoàng Phúc','fa100000-0000-4000-8000-000000000001'),
('employee.dispatch02@retrack.test','Trần Minh Quân','fa100000-0000-4000-8000-000000000001'),
('employee.dispatch03@retrack.test','Nguyễn Đức Thành','fa100000-0000-4000-8000-000000000001'),
('employee.dispatch04@retrack.test','Phạm Quốc Huy','fa100000-0000-4000-8000-000000000001'),
('employee.dispatch05@retrack.test','Võ Thanh Tùng','fa100000-0000-4000-8000-000000000001'),
('employee.dispatch06@retrack.test','Nguyễn Anh Khoa','fa100000-0000-4000-8000-000000000002'),
('employee.dispatch07@retrack.test','Đặng Thu Trang','fa100000-0000-4000-8000-000000000002'),
('employee.dispatch08@retrack.test','Hoàng Đức Hải','fa100000-0000-4000-8000-000000000002'),
('employee.dispatch09@retrack.test','Bùi Thị Hạnh','fa100000-0000-4000-8000-000000000002'),
('employee.dispatch10@retrack.test','Đỗ Thanh Bình','fa100000-0000-4000-8000-000000000002');

DO $validate$
DECLARE matched integer;
BEGIN
 IF current_setting('retrack.demo_date')::date IS NULL THEN RAISE EXCEPTION 'Ngày demo không hợp lệ'; END IF;
 SELECT count(*) INTO matched FROM defense_employees t JOIN users u ON u.email=t.email
 WHERE u.role='DEPOT_EMPLOYEE' AND u.is_active
 AND EXISTS(SELECT 1 FROM depot_staffs st WHERE st.user_id=u.id AND st.depot_id=t.depot_id AND st.staff_type='DEPOT_EMPLOYEE' AND st.is_active);
 IF matched<>10 THEN
  RAISE EXCEPTION 'Cần 10 Employee dispatch01–10 hoạt động, 01–05 thuộc Thành Đạt và 06–10 thuộc Minh Phát. DB hiện chưa có bộ seed mới: hãy dựng/chuyển DB theo bootstrap mới trước, không tự xóa dữ liệu ở đây.';
 END IF;
 SELECT count(*) INTO matched FROM pickup_requests p
 WHERE p.id BETWEEN 'fa500000-0000-4000-8000-000000000001'::uuid AND 'fa500000-0000-4000-8000-000000000100'::uuid
 AND p.target_depot_id=CASE WHEN right(p.id::text,12)::integer<=50 THEN 'fa100000-0000-4000-8000-000000000001'::uuid ELSE 'fa100000-0000-4000-8000-000000000002'::uuid END;
 IF matched<>100 THEN RAISE EXCEPTION 'Thiếu bộ 100 đơn mới tại hai kho demo (tìm thấy %). Script không tạo/xóa bộ seed cũ. Dùng bootstrap mới một lần trước khi áp lịch ngày bảo vệ.',matched; END IF;
 IF EXISTS(SELECT 1 FROM defense_employees t GROUP BY t.full_name HAVING count(*)>1) THEN RAISE EXCEPTION 'Tên Employee demo bị trùng'; END IF;
END $validate$;

-- Chỉ chuẩn hóa tên mười tài khoản demo, giữ mật khẩu, email, role và dữ liệu role khác.
UPDATE users u SET full_name=t.full_name,updated_at=CURRENT_TIMESTAMP
FROM defense_employees t WHERE u.email=t.email AND u.role='DEPOT_EMPLOYEE' AND u.full_name IS DISTINCT FROM t.full_name;


-- Nâng bộ 100 đơn lên 160 bằng 60 địa chỉ Goong bổ sung, giữ nguyên 100 đơn gốc.
INSERT INTO pickup_requests(id,seller_id,target_depot_id,description,address,latitude,longitude,
 collection_date,preferred_datetime,preferred_end_datetime,status,gross_amount,platform_fee_percentage,platform_fee_amount,net_amount,created_at,updated_at)
SELECT ('fa500000-0000-4000-8000-' || lpad((100+(v.region_index-1)*6+v.order_index-10)::text,12,'0'))::uuid,
 ('fa400000-0000-4000-8000-' || lpad(((v.region_index-1)*2+1+(v.order_index-1)%2)::text,12,'0'))::uuid,
 CASE WHEN v.region_index<=5 THEN 'fa100000-0000-4000-8000-000000000001'::uuid ELSE 'fa100000-0000-4000-8000-000000000002'::uuid END,
 (ARRAY['Tôi có vài bao vỏ chai nước và giấy bìa, đã để riêng ở cửa.','Cần thu mua sách cũ và giấy in, vui lòng báo trước khi đến.','Cửa hàng có lon nhôm sạch và vỏ hộp, đã đóng gọn trong túi.','Gia đình thay đồ dùng có chậu nhôm và mấy thanh sắt nhỏ.','Tôi có thùng carton sau khi nhận hàng, có người phụ mang ra xe.','Nhờ đến cân bao giấy vụn và chai nhựa, tôi có mặt theo giờ hẹn.'])[v.order_index-10],
 v.address,v.lat,v.lng,current_setting('retrack.demo_date')::date,t.starts,t.starts+interval '2 hours',
 'PENDING',0,5,0,0,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP
FROM (VALUES
  (1,11,'ATM Ngân hàng Phương Đông, Nguyễn Lương Bằng, Phường Hòa Khánh Bắc, Quận Liên Chiểu, Thành phố Đà Nẵng',16.0773036,108.1480878),
  (1,12,'ATM Ngân hàng Đông Á, Nguyễn Lương Bằng, Phường Hòa Khánh Bắc, Quận Liên Chiểu, Thành phố Đà Nẵng',16.0719988,108.1501861),
  (1,13,'Quán Trang, Nguyễn Lương Bằng, Phường Hòa Hiệp Nam, Quận Liên Chiểu, Thành phố Đà Nẵng',16.0917321,108.1418001),
  (1,14,'Quế Sơn quán 2, 106 Nguyễn Lương Bằng, Phường Hòa Khánh Bắc, Quận Liên Chiểu, Thành phố Đà Nẵng',16.0755235,108.1490714),
  (1,15,'Sân bóng Sư Phạm, Tôn Đức Thắng, Hòa Khánh Nam, Liên Chiểu, Đà Nẵng',16.0616806,108.1582446),
  (1,16,'Tiệm tóc A Hùng, 80 Nguyễn Lương Bằng, Phường Hòa Khánh Bắc, Quận Liên Chiểu, Thành phố Đà Nẵng',16.0744236,108.1494993),
  (2,11,'Bến Xe Trung Tâm Đà Nẵng, Tôn Đức Thắng, Liên Chiểu, Đà Nẵng',16.0565364,108.1725454),
  (2,12,'Cây Xăng, Tôn Đức Thắng, Hòa Minh, Liên Chiểu, Đà Nẵng',16.0572385,108.1717891),
  (2,13,'Cầu Đa Cô, Tôn Đức Thắng, Hòa Khánh, Đà Nẵng',16.0603151,108.1627108),
  (2,14,'Hoà Minh, Tôn Đức Thắng, Hòa Minh, Liên Chiểu, Đà Nẵng',16.0593318,108.1641698),
  (2,15,'Tôn Đức Thắng, An Khê, Đà Nẵng',16.0610319,108.1778739),
  (2,16,'Tôn Đức Thắng, Phường Hòa Minh, Quận Liên Chiểu, Thành phố Đà Nẵng',16.0598782,108.1633243),
  (3,11,'Cầu Nam Ô, Hải Vân, Đà Nẵng',16.1216089,108.1224705),
  (3,12,'Cầu đường sắt Nam Ô, Cầu Nam Ô, Liên Chiểu, Đà Nẵng',16.1211106,108.1232106),
  (3,13,'Nam Ô - P, Hòa Hiệp Nam, Liên Chiểu, Đà Nẵng',16.1083842,108.1283138),
  (3,14,'Nam ô, Hòa Hiệp Bắc, Liên Chiểu, Đà Nẵng',16.1242381,108.1223272),
  (3,15,'Nguyễn Lương Bằng, Nam Ô 1, Hải Vân, Đà Nẵng',16.1110830,108.1294404),
  (3,16,'Nguyễn Lương Bằng, Phường Hòa Hiệp Nam, Quận Liên Chiểu, Thành phố Đà Nẵng',16.0961057,108.1373637),
  (4,11,'Phòng giao dịch Cầu Vồng - Ngân hàng ACB, 308 Hùng Vương, Vĩnh Trung, Thanh Khê, Đà Nẵng',16.0672034,108.2134712),
  (4,12,'Phòng giao dịch Hùng Vương - Ngân hàng ABBank, 195 Hùng Vương, Phường Hải Châu 2, Quận Hải Châu, Thành phố Đà Nẵng',16.0675709,108.2159421),
  (4,13,'Quán Má Nụ, 200 Hùng Vương, Phường Hải Châu 2, Quận Hải Châu, Thành phố Đà Nẵng',16.0680141,108.2171979),
  (4,14,'Quán bún Bà Ba, 265 Hùng Vương, Vĩnh Trung, Thanh Khê, Đà Nẵng',16.0668462,108.2122659),
  (4,15,'Shop No Bi Na, 189 Hùng Vương, Phường Hải Châu 2, Quận Hải Châu, Thành phố Đà Nẵng',16.0676062,108.2160727),
  (4,16,'Viễn Thông A, 197 Hùng Vương, Phường Hải Châu 2, Quận Hải Châu, Thành phố Đà Nẵng',16.0675450,108.2159143),
  (5,11,'Phòng giao dịch Hùng Vương - Ngân hàng NCB, 46 Hùng Vương, Phường Hải Châu 1, Quận Hải Châu, Thành phố Đà Nẵng',16.0687054,108.2236402),
  (5,12,'Quán bún Xương, 89 Trần Phú, Phường Hải Châu 1, Quận Hải Châu, Thành phố Đà Nẵng',16.0695627,108.2241070),
  (5,13,'Sảnh Hùng Vương, Nhà hát Trưng Vương, 86 Hùng Vương, Phường Hải Châu 1, Quận Hải Châu, Thành phố Đà Nẵng',16.0686907,108.2207116),
  (5,14,'Tiệm Nhà Uyên Nhi, 215/1 Trần Phú, Phường Phước Ninh, Quận Hải Châu, Thành phố Đà Nẵng',16.0645444,108.2236059),
  (5,15,'Trần Phú, Phường Hải Châu 1, Quận Hải Châu, Thành phố Đà Nẵng',16.0716326,108.2237960),
  (5,16,'Tàu hủ xe Lam, 176 Trần Phú, Phường Phước Ninh, Quận Hải Châu, Thành phố Đà Nẵng',16.0656467,108.2234856),
  (6,11,'Cao Bá Nhạ, Phường Mân Thái, Quận Sơn Trà, Thành phố Đà Nẵng',16.0876288,108.2397881),
  (6,12,'Dã Tượng, Phường Mân Thái, Quận Sơn Trà, Thành phố Đà Nẵng',16.0903383,108.2413908),
  (6,13,'Hồ Sĩ Phấn, Phường Mân Thái, Quận Sơn Trà, Thành phố Đà Nẵng',16.0912516,108.2389901),
  (6,14,'Phường Mân Thái, Quận Sơn Trà, Thành phố Đà Nẵng',16.0893496,108.2400910),
  (6,15,'Phạm Vấn, Phường Mân Thái, Quận Sơn Trà, Thành phố Đà Nẵng',16.0868453,108.2447873),
  (6,16,'Tân An, Phường Mân Thái, Quận Sơn Trà, Thành phố Đà Nẵng',16.0900035,108.2469297),
  (7,11,'Mỹ An 21, Phường Mỹ An, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',16.0371329,108.2388667),
  (7,12,'Mỹ An 22, Phường Mỹ An, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',16.0384906,108.2404219),
  (7,13,'Mỹ An 3, Phường Mỹ An, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',16.0496102,108.2347745),
  (7,14,'Mỹ An 4, Phường Mỹ An, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',16.0490318,108.2349702),
  (7,15,'Mỹ An 5, Phường Mỹ An, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',16.0483855,108.2364963),
  (7,16,'Mỹ An 6, Phường Mỹ An, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',16.0479935,108.2354191),
  (8,11,'GAS PETROL ĐÀ NẴNG, Xô Viết Nghệ Tĩnh, Khuê Trung, Cẩm Lệ, Đà Nẵng',16.0314045,108.2117984),
  (8,12,'Huỳnh Văn Ninh, Xô Viết Nghệ Tĩnh, Hòa Cường, Hòa Cường Nam, Hải Châu, Đà Nẵng',16.0331504,108.2237515),
  (8,13,'Hải quan Đà Nẵng, 65-67 Xô Viết Nghệ Tĩnh, Phường Hòa Cường Nam, Quận Hải Châu, Thành phố Đà Nẵng',16.0316965,108.2155531),
  (8,14,'Max English Đà Nẵng, 11 Xô Viết Nghệ Tĩnh, Hoà Cường Nam, Hải Châu, Đà Nẵng',16.0326893,108.2235961),
  (8,15,'Ngã ba Xô Viết Nghệ Tĩnh, Xô Viết Nghệ Tĩnh, Khuê Trung, Cẩm Lệ, Đà Nẵng',16.0313143,108.2087868),
  (8,16,'Tòa nhà WINHOUSE Đà Nẵng, 51 Xô Viết Nghệ Tĩnh, Phường Hòa Cường Nam, Quận Hải Châu, Thành phố Đà Nẵng',16.0319890,108.2171836),
  (9,11,'Bá Tùng, Phường Hòa Quý, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',15.9924776,108.2413558),
  (9,12,'Hòa Bình 6, Khu Tái định cư Bá Tùng 2, Phường Hòa Quý, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',15.9886793,108.2318371),
  (9,13,'Hòa Bình 7, Khu Tái định cư Bá Tùng 2, Phường Hòa Quý, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',15.9886141,108.2323258),
  (9,14,'Khuê Đông, Khu Tái định cư Bá Tùng 1, Phường Hòa Quý, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',15.9862713,108.2349504),
  (9,15,'Phường Hòa Quý, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',15.9755595,108.2357244),
  (9,16,'Thái Văn A, Khu Tái định cư Bá Tùng 1, Phường Hòa Quý, Quận Ngũ Hành Sơn, Thành phố Đà Nẵng',15.9873595,108.2374414),
  (10,11,'Chợ Túy Loan, Xã Hòa Phong, Huyện Hòa Vang, Thành phố Đà Nẵng',15.9903511,108.1409952),
  (10,12,'Cầu Túy Loan, Xã Hòa Nhơn, Huyện Hòa Vang, Thành phố Đà Nẵng',15.9932341,108.1439149),
  (10,13,'Cầu Đỏ - Túy Loan, Phường Hòa Thọ Tây, Quận Cẩm Lệ, Thành phố Đà Nẵng',15.9963931,108.1652894),
  (10,14,'Làng cổ Túy Loan, Xã Hòa Phong, Huyện Hòa Vang, Thành phố Đà Nẵng',15.9975710,108.1365480),
  (10,15,'Tuý Loan, Xã Hòa Phong, Huyện Hòa Vang, Thành phố Đà Nẵng',15.9914133,108.1425754),
  (10,16,'Túy Loan Tây 2, Xã Hòa Phong, Huyện Hòa Vang, Thành phố Đà Nẵng',15.9973734,108.1303931)
) AS v(region_index,order_index,address,lat,lng)
CROSS JOIN LATERAL(SELECT (current_setting('retrack.demo_date')::date::timestamp+
 make_interval(hours=>CASE WHEN v.order_index<=14 THEN 7+2*((v.order_index-11)/2) ELSE 17 END)) AT TIME ZONE 'Asia/Ho_Chi_Minh' AS starts)t
ON CONFLICT(id) DO NOTHING;
DO $validate_expanded$
BEGIN
 IF (SELECT count(*) FROM pickup_requests p
 WHERE p.id BETWEEN 'fa500000-0000-4000-8000-000000000001'::uuid AND 'fa500000-0000-4000-8000-000000000160'::uuid
 AND p.target_depot_id=CASE WHEN right(p.id::text,12)::integer<=50 OR right(p.id::text,12)::integer BETWEEN 101 AND 130
 THEN 'fa100000-0000-4000-8000-000000000001'::uuid ELSE 'fa100000-0000-4000-8000-000000000002'::uuid END)<>160
 THEN RAISE EXCEPTION 'Bộ mở rộng 160 đơn không khớp hai kho demo; không áp lịch.'; END IF;
END $validate_expanded$;

CREATE TEMP TABLE defense_plan ON COMMIT DROP AS
SELECT p.id,p.target_depot_id,p.status,p.accepted_collector_id,p.collection_date AS old_date,
 p.preferred_datetime AS old_start,p.preferred_end_datetime AS old_end,
 current_setting('retrack.demo_date')::date AS new_date,
 CASE WHEN pos.value<4 THEN NULL::timestamptz
 ELSE (current_setting('retrack.demo_date')::date::timestamp+
  make_interval(hours=>CASE WHEN pos.value<10 THEN 11+2*((pos.value-4)/2) WHEN pos.value<14 THEN 7+2*((pos.value-10)/2) ELSE 17 END)) AT TIME ZONE 'Asia/Ho_Chi_Minh' END AS new_start,
 p.requires_owner_review,p.dispatch_review_reason,
 p.status IN ('PENDING','SCHEDULED') AND nullif(btrim(p.checkin_image_url),'') IS NULL
 AND NOT EXISTS(SELECT 1 FROM pickup_checkins c WHERE c.pickup_request_id=p.id) AS can_move
FROM pickup_requests p
CROSS JOIN LATERAL(SELECT CASE WHEN right(p.id::text,12)::integer<=100 THEN (right(p.id::text,12)::integer-1)%10 ELSE (right(p.id::text,12)::integer-101)%6+10 END AS value)pos
WHERE p.id BETWEEN 'fa500000-0000-4000-8000-000000000001'::uuid AND 'fa500000-0000-4000-8000-000000000160'::uuid;
ALTER TABLE defense_plan ADD COLUMN new_end timestamptz;
ALTER TABLE defense_plan ADD COLUMN review boolean;
UPDATE defense_plan SET new_end=coalesce(new_start+interval '2 hours',
 (new_date::timestamp+interval '19 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh');
UPDATE defense_plan SET review=new_end<=CURRENT_TIMESTAMP;

-- Không reset đơn đã check-in, đã cân, thanh toán, DONE/CANCELLED... Không dời lịch sử.
CREATE TEMP TABLE defense_changed ON COMMIT DROP AS
SELECT * FROM defense_plan WHERE can_move AND
 (old_date IS DISTINCT FROM new_date OR old_start IS DISTINCT FROM new_start OR old_end IS DISTINCT FROM (CASE WHEN new_start IS NULL THEN NULL ELSE new_end END)
 OR requires_owner_review IS DISTINCT FROM review);
INSERT INTO employee_dispatch_events(id,pickup_id,from_employee_id,to_employee_id,reason,created_at)
SELECT gen_random_uuid(),id,accepted_collector_id,NULL,
 format('Chuẩn bị lịch demo: %s %s–%s → %s %s–%s (giờ Việt Nam); giữ nguyên bằng chứng và tài chính.',old_date,old_start AT TIME ZONE 'Asia/Ho_Chi_Minh',old_end AT TIME ZONE 'Asia/Ho_Chi_Minh',new_date,new_start AT TIME ZONE 'Asia/Ho_Chi_Minh',new_end AT TIME ZONE 'Asia/Ho_Chi_Minh'),CURRENT_TIMESTAMP
FROM defense_changed;
UPDATE pickup_requests p SET collection_date=t.new_date,preferred_datetime=t.new_start,
 preferred_end_datetime=CASE WHEN t.new_start IS NULL THEN NULL ELSE t.new_end END,
 accepted_collector_id=NULL,status='PENDING',requires_owner_review=t.review,
 dispatch_review_reason=CASE WHEN t.review THEN 'Khung thu gom của ngày demo đã kết thúc; chờ chủ kho xác nhận lịch mới.' ELSE NULL END,
 updated_at=CURRENT_TIMESTAMP
FROM defense_changed t WHERE p.id=t.id;
INSERT INTO employee_dispatch_queue(pickup_id,reason,updated_at)
SELECT id,CASE WHEN review THEN 'Khung thu gom của ngày demo đã kết thúc; chờ chủ kho xác nhận lịch mới.' ELSE 'Đã chuẩn bị lịch demo, chờ hệ thống phân công đúng ngày.' END,CURRENT_TIMESTAMP
FROM defense_changed
ON CONFLICT(pickup_id) DO UPDATE SET reason=EXCLUDED.reason,updated_at=EXCLUDED.updated_at;

-- Chỉ reset ca TỰ ĐỘNG của ngày demo, đúng hai kho và mười người, không đổi ca cũ/người ngoài fixture.
-- Không reset ca của người đang giữ đơn đã bắt đầu ở cùng kho.
CREATE TEMP TABLE defense_shifts ON COMMIT DROP AS
SELECT sh.id,sh.employee_id,sh.availability,sh.reason,sh.starts_at,sh.ends_at,a.depot_id
FROM employee_dispatch_shifts sh JOIN employee_collection_areas a ON a.id=sh.area_id
JOIN users u ON u.id=sh.employee_id JOIN defense_employees t ON t.email=u.email AND t.depot_id=a.depot_id
WHERE sh.is_automatic AND sh.starts_at<((current_setting('retrack.demo_date')::date+1)::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
 AND sh.ends_at>(current_setting('retrack.demo_date')::date::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
 AND NOT EXISTS(SELECT 1 FROM pickup_requests p WHERE p.accepted_collector_id=sh.employee_id AND p.target_depot_id=a.depot_id
  AND p.status IN ('IN_PROGRESS','WEIGHED','SELLER_CONFIRMED','AWAITING_PAYMENT','PAYMENT_SENT'));
-- Lưu dấu trạng thái ca trước khi chuẩn bị lại để không mất lý do STOPPED/FULL.
INSERT INTO employee_dispatch_events(id,pickup_id,from_employee_id,to_employee_id,shift_id,reason,created_at)
SELECT gen_random_uuid(),p.id,t.employee_id,NULL,t.id,
 format('Quản trị chuẩn bị lại ca demo; trạng thái ca trước: %s; lý do trước: %s; giờ ca trước: %s–%s.',t.availability,t.reason,t.starts_at,t.ends_at),CURRENT_TIMESTAMP
FROM defense_shifts t JOIN LATERAL(SELECT id FROM defense_plan WHERE can_move AND target_depot_id=t.depot_id ORDER BY id LIMIT 1)p ON true
WHERE t.availability<>'AVAILABLE' OR t.starts_at IS DISTINCT FROM ((current_setting('retrack.demo_date')::date::timestamp+interval '7 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh')
 OR t.ends_at IS DISTINCT FROM ((current_setting('retrack.demo_date')::date::timestamp+interval '19 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh');
UPDATE employee_dispatch_shifts sh SET availability='AVAILABLE',
 starts_at=(current_setting('retrack.demo_date')::date::timestamp+interval '7 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh',
 ends_at=(current_setting('retrack.demo_date')::date::timestamp+interval '19 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh',
 reason='Chuẩn bị ca demo 07:00–19:00',updated_at=CURRENT_TIMESTAMP
FROM defense_shifts t WHERE sh.id=t.id;

SELECT current_database() AS database_applied,current_setting('retrack.demo_date')::date AS demo_date,
 (SELECT count(*) FROM defense_changed) AS orders_rescheduled,
 (SELECT count(*) FROM defense_plan WHERE NOT can_move) AS protected_orders_not_reset;
-- Nếu protected_orders_not_reset > 0 thì không kỳ vọng đủ 160 đơn mới: xem danh sách sau.
SELECT p.id,p.status,p.old_date,'Giữ nguyên vì đã bắt đầu/kết thúc; không reset bằng chứng hoặc thanh toán' AS note
FROM defense_plan p WHERE NOT p.can_move ORDER BY p.id;
SELECT collection_date,preferred_datetime AT TIME ZONE 'Asia/Ho_Chi_Minh' AS start_vn,
 preferred_end_datetime AT TIME ZONE 'Asia/Ho_Chi_Minh' AS end_vn,count(*)
FROM pickup_requests WHERE id IN (SELECT id FROM defense_plan) GROUP BY collection_date,preferred_datetime,preferred_end_datetime
ORDER BY collection_date,preferred_datetime NULLS FIRST;
COMMIT;
