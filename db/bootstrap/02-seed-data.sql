-- Dữ liệu demo dùng cục bộ/phát triển, không dùng production.
-- Máy mới: chạy db/bootstrap/01-retrack-system.sql trước; mở file này trong Query Tool pgAdmin, nhấn F5.
-- Có thể chạy lại; các UUID fixture đã có sẽ được giữ nguyên.
-- Tài khoản demo được tạo khi email/ID chưa tồn tại; mật khẩu chỉ lưu dạng BCrypt.
-- Không dùng thông tin demo cho môi trường thật.

BEGIN;

-- Tài khoản đăng nhập cho Admin, bốn Seller, Depot Owner, hai Employee, hai Driver và ba Factory.
INSERT INTO users (id, email, password_hash, role, full_name, phone, is_active, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000001', 'admin@retrack.vn',    '$2a$11$eNEiPwGZnOqVlHiSydDe4exEtVDa5h2t8vR.VBK/tTAv/W1mHBOwW', 'ADMIN',          'Quản trị viên ReTrack', '0900000001', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000002', 'seller@retrack.vn',   '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Nguyễn Thị Lan',        '0911111111', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000003', 'depot@retrack.vn',    '$2a$11$i0GJ0.h60xsCKWZ8XvYkJ.guY1aYe9ykxcsg22/KvM4PZ7kXr6Sce', 'DEPOT_OWNER',    'Ngô Sỹ Giá',             '0922222222', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000004', 'employee@retrack.vn', '$2a$11$7cergszHVwtBQKDOOIM.POdnPLveC9s7edhE2hY6/rStLyg.889hm', 'DEPOT_EMPLOYEE', 'Lê Minh Tuấn',           '0933333333', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000005', 'driver@retrack.vn',   '$2a$11$d7vd3QQIgwwE8U/3VvpwGuThZU0vhoxsA2jrzvkQ5qkL3KiWgx3UG', 'DRIVER',         'Phạm Văn Hùng',          '0944444444', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000006', 'factory@retrack.vn',  '$2a$11$mIHozvQGf7pIuXnWk4j03OKdxo7q27HVkx3LzqUu3cm2rrhjNCzmG', 'FACTORY',        'Eco Plastics Vietnam',   '0955555551', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000007', 'factory2@retrack.vn', '$2a$11$mIHozvQGf7pIuXnWk4j03OKdxo7q27HVkx3LzqUu3cm2rrhjNCzmG', 'FACTORY',        'Công Ty Kim Loại Phú Lợi','0955555552', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000008', 'factory3@retrack.vn', '$2a$11$mIHozvQGf7pIuXnWk4j03OKdxo7q27HVkx3LzqUu3cm2rrhjNCzmG', 'FACTORY',        'Nhà Máy Giấy Miền Trung','0955555553', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000009', 'seller2@retrack.vn',  '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Trần Thị Hương',        '0911111112', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000a', 'seller3@retrack.vn',  '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Phạm Minh Khoa',        '0911111113', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000b', 'seller4@retrack.vn',  '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Lê Thanh Mai',          '0911111114', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000c', 'employee2@retrack.vn','$2a$11$7cergszHVwtBQKDOOIM.POdnPLveC9s7edhE2hY6/rStLyg.889hm', 'DEPOT_EMPLOYEE', 'Nguyễn Quốc Bảo',       '0933333334', TRUE, NOW(), NOW()),
    ('00000000-0000-0000-0000-00000000000d', 'driver2@retrack.vn',  '$2a$11$d7vd3QQIgwwE8U/3VvpwGuThZU0vhoxsA2jrzvkQ5qkL3KiWgx3UG', 'DRIVER',         'Trần Văn Hải',          '0944444445', TRUE, NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Chặn việc âm thầm nối dữ liệu demo vào tài khoản cùng email nhưng sai ID/role.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM (VALUES
            ('00000000-0000-0000-0000-000000000001'::uuid, 'admin@retrack.vn', 'ADMIN'),
            ('00000000-0000-0000-0000-000000000002'::uuid, 'seller@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-000000000003'::uuid, 'depot@retrack.vn', 'DEPOT_OWNER'),
            ('00000000-0000-0000-0000-000000000004'::uuid, 'employee@retrack.vn', 'DEPOT_EMPLOYEE'),
            ('00000000-0000-0000-0000-000000000005'::uuid, 'driver@retrack.vn', 'DRIVER'),
            ('00000000-0000-0000-0000-000000000006'::uuid, 'factory@retrack.vn', 'FACTORY'),
            ('00000000-0000-0000-0000-000000000007'::uuid, 'factory2@retrack.vn', 'FACTORY'),
            ('00000000-0000-0000-0000-000000000008'::uuid, 'factory3@retrack.vn', 'FACTORY'),
            ('00000000-0000-0000-0000-000000000009'::uuid, 'seller2@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-00000000000a'::uuid, 'seller3@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-00000000000b'::uuid, 'seller4@retrack.vn', 'SELLER'),
            ('00000000-0000-0000-0000-00000000000c'::uuid, 'employee2@retrack.vn', 'DEPOT_EMPLOYEE'),
            ('00000000-0000-0000-0000-00000000000d'::uuid, 'driver2@retrack.vn', 'DRIVER')
        ) AS expected(id, email, role)
        LEFT JOIN users u ON u.id = expected.id AND u.email = expected.email AND u.role = expected.role
        WHERE u.id IS NULL
    ) THEN
        RAISE EXCEPTION 'Một tài khoản demo đã tồn tại với ID/email/role khác. Không tiếp tục seed để tránh nối nhầm dữ liệu.';
    END IF;
END
$$;

INSERT INTO depots (id, owner_id, name, address, latitude, longitude, rating, contact_phone, description, created_at)
VALUES ('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000003',
        'Kho vựa phế liệu Ngô Sỹ Giá', 'Khu vực X7P4+XF9, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)',
        15.9874125, 108.2562344, 0, '0922222222', 'Kho mẫu phát triển tại Đà Nẵng.', NOW())
ON CONFLICT DO NOTHING;

INSERT INTO depots (id, owner_id, name, address, latitude, longitude, rating, contact_phone, description, created_at)
VALUES
    ('00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000003',
     'Điểm thu gom Hải Châu', 'Khu vực Chợ Hàn, 119 Trần Phú, Hải Châu, Đà Nẵng (địa điểm demo)',
     16.0683600, 108.2244300, 0, '0922222222', 'Điểm demo khu vực trung tâm Đà Nẵng.', NOW()),
    ('00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000003',
     'Kho thu gom Cẩm Lệ', 'Khu vực Cẩm Lệ, Đà Nẵng (địa điểm demo)',
     16.0150000, 108.2100000, 0, '0922222222', 'Kho demo phía nam trung tâm Đà Nẵng.', NOW())
ON CONFLICT DO NOTHING;

INSERT INTO depot_staffs (id, depot_id, user_id, staff_type, is_active)
VALUES
    ('00000000-0000-0000-0000-000000000030', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000031', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000005', 'DRIVER', TRUE),
    ('00000000-0000-0000-0000-000000000032', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000033', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000005', 'DRIVER', TRUE),
    ('00000000-0000-0000-0000-000000000034', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000004', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000035', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000005', 'DRIVER', TRUE),
    ('00000000-0000-0000-0000-000000000036', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c', 'DEPOT_EMPLOYEE', TRUE),
    ('00000000-0000-0000-0000-000000000037', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-00000000000d', 'DRIVER', TRUE)
ON CONFLICT DO NOTHING;

-- Mỗi nhà máy có mã vật liệu chuẩn đúng với MaterialCatalog của API.
INSERT INTO factories (id, owner_id, name, address, latitude, longitude, rating, contact_phone,
                       capacity_kg_per_month, minimum_purity_percent, accepted_materials, created_at)
VALUES
    ('00000000-0000-0000-0000-000000000020', '00000000-0000-0000-0000-000000000006', 'Eco Plastics Vietnam',
     'Khu đô thị FPT, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)', 15.9832468, 108.2520905, 0,
     '0955555551', 20000, 0, 'PET,HDPE', NOW()),
    ('00000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000007', 'Công Ty Kim Loại Phú Lợi',
     'Khu vực Hòa Khánh, Đà Nẵng (địa điểm demo)', 16.0670000, 108.1500000, 0,
     '0955555552', 20000, 0, 'ALUMINUM,IRON,STEEL,COPPER', NOW()),
    ('00000000-0000-0000-0000-000000000022', '00000000-0000-0000-0000-000000000008', 'Nhà Máy Giấy Miền Trung',
     'Khu vực Cẩm Lệ, Đà Nẵng (địa điểm demo)', 16.0150000, 108.2100000, 0,
     '0955555553', 20000, 0, 'PAPER,CARDBOARD', NOW())
ON CONFLICT DO NOTHING;

INSERT INTO factory_demands (id, factory_id, material_type, required_weight_kg, min_price_per_kg,
                             max_price_per_kg, deadline, is_active, note, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000040', '00000000-0000-0000-0000-000000000020', 'PET', 5000, 8000, 12000, NOW() + INTERVAL '30 days', TRUE, 'Nhu cầu demo PET.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000041', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 3000, 20000, 30000, NOW() + INTERVAL '30 days', TRUE, 'Nhu cầu demo nhôm.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000042', '00000000-0000-0000-0000-000000000022', 'CARDBOARD', 4000, 2000, 4000, NOW() + INTERVAL '30 days', TRUE, 'Nhu cầu demo bìa carton.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000043', '00000000-0000-0000-0000-000000000020', 'HDPE', 2500, 9000, 14000, NOW() + INTERVAL '14 days', TRUE, 'Nhu cầu demo nhựa HDPE.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000044', '00000000-0000-0000-0000-000000000021', 'COPPER', 1200, 58000, 70000, NOW() + INTERVAL '21 days', TRUE, 'Nhu cầu demo đồng.', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000045', '00000000-0000-0000-0000-000000000022', 'PAPER', 3200, 2500, 3800, NOW() + INTERVAL '10 days', TRUE, 'Nhu cầu demo giấy.', NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Nhiều trạng thái từ yêu cầu mới đến hoàn tất để các role thử toàn bộ danh sách và tồn kho.
INSERT INTO pickup_requests (id, seller_id, target_depot_id, accepted_collector_id, description, address,
    latitude, longitude, preferred_datetime, gross_amount, platform_fee_percentage, platform_fee_amount,
    net_amount, status, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001001', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', NULL,
     '[DEMO] Yêu cầu mới để thử Seller → Employee.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() + INTERVAL '2 hours', 0, 5, 0, 0, 'PENDING', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001002', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu đã nhận để thử hồ sơ Employee.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() + INTERVAL '4 hours', 0, 5, 0, 0, 'SCHEDULED', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001003', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu hoàn tất để tạo tồn kho PET mẫu; không phải giao dịch thật.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() - INTERVAL '1 day', 2000000, 5, 100000, 1900000, 'DONE', NOW() - INTERVAL '2 days', NOW() - INTERVAL '1 day'),
    ('00000000-0000-0000-0000-000000001004', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu chờ chủ kho thanh toán cho Seller.', 'X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng', 15.9874125, 108.2562344,
     NOW() - INTERVAL '2 hours', 500000, 5, 25000, 475000, 'AWAITING_PAYMENT', NOW() - INTERVAL '1 day', NOW()),
    ('00000000-0000-0000-0000-000000001005', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', NULL,
     '[DEMO] Yêu cầu Seller mới tại Hải Châu.', 'Chợ Hàn, 119 Trần Phú, Hải Châu, Đà Nẵng', 16.0683600, 108.2244300,
     NOW() + INTERVAL '1 day', 0, 5, 0, 0, 'PENDING', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001006', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000012', NULL,
     '[DEMO] Yêu cầu nhôm mới tại Cẩm Lệ.', 'Khu vực Cẩm Lệ, Đà Nẵng', 16.0150000, 108.2100000,
     NOW() + INTERVAL '3 hours', 0, 5, 0, 0, 'PENDING', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001007', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Yêu cầu đã nhận để kiểm tra lịch Employee.', 'Bảo tàng Điêu khắc Chăm, 2 Tháng 9, Hải Châu, Đà Nẵng', 16.0602000, 108.2231700,
     NOW() + INTERVAL '6 hours', 0, 5, 0, 0, 'SCHEDULED', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001008', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Hoàn tất thu gom bìa carton để tạo tồn kho mẫu.', 'Khu vực Cẩm Lệ, Đà Nẵng', 16.0150000, 108.2100000,
     NOW() - INTERVAL '3 days', 320000, 5, 16000, 304000, 'DONE', NOW() - INTERVAL '4 days', NOW() - INTERVAL '3 days'),
    ('00000000-0000-0000-0000-000000001009', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Hoàn tất thu gom nhôm để tạo tồn kho mẫu.', 'Chợ Cồn, 290 Hùng Vương, Hải Châu, Đà Nẵng', 16.0681700, 108.2145300,
     NOW() - INTERVAL '5 days', 1680000, 5, 84000, 1596000, 'DONE', NOW() - INTERVAL '6 days', NOW() - INTERVAL '5 days'),
    ('00000000-0000-0000-0000-000000001021', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c',
     '[DEMO] Đã cân PET, chờ Seller xác nhận.', 'Khu vực Mỹ An, Ngũ Hành Sơn, Đà Nẵng', 16.0355000, 108.2453000,
     NOW() - INTERVAL '3 hours', 600000, 5, 30000, 570000, 'WEIGHED', NOW() - INTERVAL '1 day', NOW()),
    ('00000000-0000-0000-0000-000000001022', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c',
     '[DEMO] Seller đã duyệt cân HDPE, chờ chủ kho xử lý.', 'Khu vực Khuê Mỹ, Ngũ Hành Sơn, Đà Nẵng', 16.0127000, 108.2544000,
     NOW() - INTERVAL '1 day', 420000, 5, 21000, 399000, 'SELLER_CONFIRMED', NOW() - INTERVAL '2 days', NOW()),
    ('00000000-0000-0000-0000-000000001023', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Chủ kho đã gửi tiền, chờ Seller xác nhận.', 'Khu vực Thanh Bình, Hải Châu, Đà Nẵng', 16.0770000, 108.2070000,
     NOW() - INTERVAL '2 days', 360000, 5, 18000, 342000, 'PAYMENT_SENT', NOW() - INTERVAL '3 days', NOW()),
    ('00000000-0000-0000-0000-000000001024', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-00000000000c',
     '[DEMO] PET đã hoàn tất, bổ sung tồn kho cho lô xuất.', 'Khu vực Hòa Quý, Ngũ Hành Sơn, Đà Nẵng', 15.9830000, 108.2530000,
     NOW() - INTERVAL '6 days', 2000000, 5, 100000, 1900000, 'DONE', NOW() - INTERVAL '7 days', NOW() - INTERVAL '6 days'),
    ('00000000-0000-0000-0000-000000001025', '00000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Nhôm đã hoàn tất, bổ sung tồn kho cho lô xuất.', 'Khu vực Hòa Cường, Hải Châu, Đà Nẵng', 16.0370000, 108.2210000,
     NOW() - INTERVAL '8 days', 4200000, 5, 210000, 3990000, 'DONE', NOW() - INTERVAL '9 days', NOW() - INTERVAL '8 days'),
    ('00000000-0000-0000-0000-000000001026', '00000000-0000-0000-0000-00000000000a', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000004',
     '[DEMO] Carton đã hoàn tất, bổ sung tồn kho cho lô xuất.', 'Khu vực Hòa Xuân, Cẩm Lệ, Đà Nẵng', 16.0010000, 108.2110000,
     NOW() - INTERVAL '10 days', 480000, 5, 24000, 456000, 'DONE', NOW() - INTERVAL '11 days', NOW() - INTERVAL '10 days'),
    ('00000000-0000-0000-0000-000000001027', '00000000-0000-0000-0000-00000000000b', '00000000-0000-0000-0000-000000000010', NULL,
     '[DEMO] Yêu cầu mới để kiểm tra danh sách có phân trang.', 'Khu vực Non Nước, Ngũ Hành Sơn, Đà Nẵng', 15.9990000, 108.2740000,
     NOW() + INTERVAL '2 days', 0, 5, 0, 0, 'PENDING', NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO pickup_request_items (id, pickup_request_id, material_type, weight_kg, price_per_kg, sub_total)
VALUES
    ('00000000-0000-0000-0000-000000001011', '00000000-0000-0000-0000-000000001003', 'PET', 200, 10000, 2000000),
    ('00000000-0000-0000-0000-000000001012', '00000000-0000-0000-0000-000000001004', 'PET', 50, 10000, 500000),
    ('00000000-0000-0000-0000-000000001013', '00000000-0000-0000-0000-000000001008', 'CARDBOARD', 80, 4000, 320000),
    ('00000000-0000-0000-0000-000000001014', '00000000-0000-0000-0000-000000001009', 'ALUMINUM', 60, 28000, 1680000),
    ('00000000-0000-0000-0000-000000001031', '00000000-0000-0000-0000-000000001021', 'PET', 60, 10000, 600000),
    ('00000000-0000-0000-0000-000000001032', '00000000-0000-0000-0000-000000001022', 'HDPE', 35, 12000, 420000),
    ('00000000-0000-0000-0000-000000001033', '00000000-0000-0000-0000-000000001023', 'PAPER', 120, 3000, 360000),
    ('00000000-0000-0000-0000-000000001034', '00000000-0000-0000-0000-000000001024', 'PET', 200, 10000, 2000000),
    ('00000000-0000-0000-0000-000000001035', '00000000-0000-0000-0000-000000001025', 'ALUMINUM', 150, 28000, 4200000),
    ('00000000-0000-0000-0000-000000001036', '00000000-0000-0000-0000-000000001026', 'CARDBOARD', 120, 4000, 480000)
ON CONFLICT DO NOTHING;

-- Một lời mời trực tiếp chờ nhà máy quyết định lô đầu tiên; không seed APPROVED.
INSERT INTO factory_depot_partnerships (id, depot_id, factory_id, status, blocked_by_factory, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000050', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PENDING', FALSE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000051', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'APPROVED', FALSE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000052', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000022', 'PENDING', FALSE, NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000053', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000021', 'BLOCKED', TRUE, NOW(), NOW())
ON CONFLICT (depot_id, factory_id) DO NOTHING;

INSERT INTO inventory_batches (id, code, depot_id, direct_offer_factory_id, material_type, declared_weight_kg,
                               description, status, image_urls, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001101', 'LO-DEMO-FACTORY-01',
        '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PET', 100,
        '[DEMO] Lô đầu tiên chờ Eco Plastics nhận; sau QC nhà máy mới quyết định hợp tác lâu dài.',
        'PENDING_APPROVAL', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Hai lô công khai ở hai kho khác nhau, vật liệu khớp danh mục nhận của từng Factory.
INSERT INTO inventory_batches (id, code, depot_id, material_type, declared_weight_kg, description, status, image_urls, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001102', 'LO-DEMO-CARD-01', '00000000-0000-0000-0000-000000000012', 'CARDBOARD', 50,
     '[DEMO] Bìa carton sạch, lô công khai để thử lọc vật liệu và nhu cầu Factory.', 'MARKETPLACE', ARRAY[]::TEXT[], NOW(), NOW()),
    ('00000000-0000-0000-0000-000000001103', 'LO-DEMO-AL-01', '00000000-0000-0000-0000-000000000011', 'ALUMINUM', 40,
     '[DEMO] Nhôm phân loại, lô công khai để thử Factory nhận mua.', 'MARKETPLACE', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO inventory_batches (id, code, depot_id, target_factory_id, material_type, declared_weight_kg, description, status, image_urls, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001104', 'LO-DEMO-AL-02',
        '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 20,
        '[DEMO] Lô demo đã được Factory nhận theo quan hệ hợp tác có sẵn, sẵn sàng giao tài xế.',
        'TRANSPORT_READY', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO inventory_batches (id, code, depot_id, direct_offer_factory_id, material_type, declared_weight_kg, description, status, image_urls, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001105', 'LO-DEMO-CARD-02',
        '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000022', 'CARDBOARD', 30,
        '[DEMO] Lô carton đầu tiên được chỉ định Nhà Máy Giấy Miền Trung, chờ nhà máy nhận.',
        'PENDING_APPROVAL', ARRAY[]::TEXT[], NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO transport_jobs (id, batch_id, status, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000001201', '00000000-0000-0000-0000-000000001104', 'PENDING', NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Lịch sử đã giao, đã nhận, cân/KCS và quyết toán để Depot/Factory xem báo cáo.
INSERT INTO inventory_batches (id, code, depot_id, target_factory_id, material_type, declared_weight_kg,
    description, status, actual_weight_kg, factory_received_at, factory_decided_at,
    agreed_price_per_kg, gross_amount, platform_fee_amount, net_amount, payment_reference, settled_at,
    image_urls, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001111', 'LO-DEMO-PET-QC', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PET', 80,
     '[DEMO] Đã nhận hàng, đang chờ cân tại nhà máy.', 'RECEIVED', NULL, NOW() - INTERVAL '2 days', NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days'),
    ('00000000-0000-0000-0000-000000001112', 'LO-DEMO-AL-KCS', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 40,
     '[DEMO] Đã cân, chờ nhà máy kết luận KCS.', 'WEIGHED', 39, NOW() - INTERVAL '4 days', NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001113', 'LO-DEMO-CARD-VER', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000022', 'CARDBOARD', 35,
     '[DEMO] Đã đạt KCS, đang chờ nhà máy quyết toán.', 'VERIFIED', 34, NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days',
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '6 days', NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001114', 'LO-DEMO-PET-PAID', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PET', 70,
     '[DEMO] Đã quyết toán với Factory, dùng cho báo cáo doanh thu.', 'COMPLETED', 69, NOW() - INTERVAL '9 days', NOW() - INTERVAL '8 days',
     10000, 690000, 34500, 655500, 'DEMO-FACTORY-PET-01', NOW() - INTERVAL '7 days', ARRAY[]::TEXT[], NOW() - INTERVAL '10 days', NOW() - INTERVAL '7 days'),
    ('00000000-0000-0000-0000-000000001115', 'LO-DEMO-AL-REJ', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'ALUMINUM', 30,
     '[DEMO] Lô bị từ chối KCS; hàng chưa được ghi nhận trả về kho.', 'REJECTED', 29, NOW() - INTERVAL '7 days', NOW() - INTERVAL '6 days',
     NULL, NULL, NULL, NULL, NULL, NULL, ARRAY[]::TEXT[], NOW() - INTERVAL '8 days', NOW() - INTERVAL '6 days')
ON CONFLICT DO NOTHING;

INSERT INTO transport_jobs (id, batch_id, driver_id, status, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000001211', '00000000-0000-0000-0000-000000001111', '00000000-0000-0000-0000-000000000005', 'DELIVERED', NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days'),
    ('00000000-0000-0000-0000-000000001212', '00000000-0000-0000-0000-000000001112', '00000000-0000-0000-0000-00000000000d', 'DELIVERED', NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001213', '00000000-0000-0000-0000-000000001113', '00000000-0000-0000-0000-000000000005', 'DELIVERED', NOW() - INTERVAL '6 days', NOW() - INTERVAL '5 days'),
    ('00000000-0000-0000-0000-000000001214', '00000000-0000-0000-0000-000000001114', '00000000-0000-0000-0000-000000000005', 'DELIVERED', NOW() - INTERVAL '10 days', NOW() - INTERVAL '9 days'),
    ('00000000-0000-0000-0000-000000001215', '00000000-0000-0000-0000-000000001115', '00000000-0000-0000-0000-00000000000d', 'DELIVERED', NOW() - INTERVAL '8 days', NOW() - INTERVAL '7 days')
ON CONFLICT DO NOTHING;

INSERT INTO batch_quality_checks (id, batch_id, factory_id, actual_weight_kg, grade, agreed_price_per_kg,
    gross_amount, platform_fee_percentage, platform_fee_amount, net_amount, is_accepted,
    gross_weight_kg, tare_weight_kg, difference_percentage, ticket_number, purity_percent,
    moisture_percent, contamination_percent, quality_note, resolution, invoice_number, invoice_status, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001221', '00000000-0000-0000-0000-000000001112', '00000000-0000-0000-0000-000000000021', 39, 'PENDING', 0,
     0, 0, 0, 0, FALSE, 45, 6, -2.50, 'DEMO-CAN-AL-01', NULL, NULL, NULL,
     '[DEMO] Đã cân, chờ kết luận KCS.', NULL, NULL, NULL, NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001222', '00000000-0000-0000-0000-000000001113', '00000000-0000-0000-0000-000000000022', 34, 'A', 0,
     0, 0, 0, 0, TRUE, 39, 5, -2.86, 'DEMO-CAN-CARD-01', 95, 3, 2,
     '[DEMO] Bìa carton đạt yêu cầu.', NULL, NULL, NULL, NOW() - INTERVAL '4 days'),
    ('00000000-0000-0000-0000-000000001223', '00000000-0000-0000-0000-000000001114', '00000000-0000-0000-0000-000000000020', 69, 'A', 10000,
     690000, 5, 34500, 655500, TRUE, 75, 6, -1.43, 'DEMO-CAN-PET-01', 97, 2, 1,
     '[DEMO] PET đã đạt KCS và quyết toán; nhà máy có thể tải hóa đơn qua giao diện.', NULL, NULL, NULL, NOW() - INTERVAL '8 days'),
    ('00000000-0000-0000-0000-000000001224', '00000000-0000-0000-0000-000000001115', '00000000-0000-0000-0000-000000000021', 29, 'C', 0,
     0, 0, 0, 0, FALSE, 35, 6, -3.33, 'DEMO-CAN-AL-02', 72, 10, 18,
     '[DEMO] Lẫn tạp chất; chờ hai bên thống nhất hướng trả hàng.', 'RETURN', NULL, NULL, NOW() - INTERVAL '6 days')
ON CONFLICT DO NOTHING;

INSERT INTO notifications (id, user_id, title, message, is_read, created_at, transport_job_id)
VALUES ('00000000-0000-0000-0000-000000001301', '00000000-0000-0000-0000-000000000005',
        'Chuyến xe demo sẵn sàng', 'Lô LO-DEMO-AL-02 tại điểm thu gom Hải Châu đang chờ tài xế nhận.', FALSE, NOW(),
        '00000000-0000-0000-0000-000000001201')
ON CONFLICT DO NOTHING;

-- Giá tham khảo phát triển cho Admin/Depot/Factory; nguồn được ghi rõ là mẫu.
INSERT INTO market_prices (id, material_type, price_per_kg, effective_date, source, created_at)
VALUES
    ('00000000-0000-0000-0000-000000000060', 'PET', 10000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000061', 'HDPE', 12000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000062', 'PAPER', 3000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000063', 'CARDBOARD', 2500, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000064', 'ALUMINUM', 28000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000065', 'IRON', 12000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000066', 'STEEL', 15000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW()),
    ('00000000-0000-0000-0000-000000000067', 'COPPER', 65000, CURRENT_DATE, 'Dữ liệu demo — Admin cần xác minh', NOW())
ON CONFLICT DO NOTHING;

-- Seller/Factory đánh giá sau các giao dịch đã hoàn tất; dữ liệu này hiện lên hồ sơ đối tác.
INSERT INTO seller_depot_reviews (id, pickup_request_id, depot_id, rating, comment, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001401', '00000000-0000-0000-0000-000000001003', '00000000-0000-0000-0000-000000000010', 5, '[DEMO] Nhân viên thu gom đúng giờ.', NOW() - INTERVAL '1 day'),
    ('00000000-0000-0000-0000-000000001402', '00000000-0000-0000-0000-000000001025', '00000000-0000-0000-0000-000000000011', 4, '[DEMO] Cân và thanh toán rõ ràng.', NOW() - INTERVAL '7 days'),
    ('00000000-0000-0000-0000-000000001403', '00000000-0000-0000-0000-000000001026', '00000000-0000-0000-0000-000000000012', 5, '[DEMO] Thu gom nhanh.', NOW() - INTERVAL '9 days')
ON CONFLICT DO NOTHING;

INSERT INTO factory_depot_reviews (id, batch_id, factory_id, depot_id, rating, comment, created_at)
VALUES ('00000000-0000-0000-0000-000000001411', '00000000-0000-0000-0000-000000001114',
        '00000000-0000-0000-0000-000000000020', '00000000-0000-0000-0000-000000000010', 5,
        '[DEMO] PET được phân loại sạch, đúng mô tả.', NOW() - INTERVAL '6 days')
ON CONFLICT DO NOTHING;

-- Ghi phí đúng một lần cho mỗi nguồn đã thanh toán, phục vụ báo cáo Admin/Depot.
INSERT INTO platform_transactions (id, source_type, source_id, fee_amount, payer_id, transaction_amount,
                                   fee_percentage, description, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001501', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001003', 100000,
     '00000000-0000-0000-0000-000000000003', 2000000, 5, '[DEMO] Phí thu gom PET đã hoàn tất.', NOW() - INTERVAL '1 day'),
    ('00000000-0000-0000-0000-000000001502', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001025', 210000,
     '00000000-0000-0000-0000-000000000003', 4200000, 5, '[DEMO] Phí thu gom nhôm đã hoàn tất.', NOW() - INTERVAL '8 days'),
    ('00000000-0000-0000-0000-000000001503', 'BATCH_ORDER', '00000000-0000-0000-0000-000000001114', 34500,
     '00000000-0000-0000-0000-000000000006', 690000, 5, '[DEMO] Phí quyết toán lô PET.', NOW() - INTERVAL '7 days'),
    ('00000000-0000-0000-0000-000000001504', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001008', 16000,
     '00000000-0000-0000-0000-000000000003', 320000, 5, '[DEMO] Phí thu gom carton đã hoàn tất.', NOW() - INTERVAL '3 days'),
    ('00000000-0000-0000-0000-000000001505', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001009', 84000,
     '00000000-0000-0000-0000-000000000003', 1680000, 5, '[DEMO] Phí thu gom nhôm đã hoàn tất.', NOW() - INTERVAL '5 days'),
    ('00000000-0000-0000-0000-000000001506', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001024', 100000,
     '00000000-0000-0000-0000-000000000003', 2000000, 5, '[DEMO] Phí thu gom PET đã hoàn tất.', NOW() - INTERVAL '6 days'),
    ('00000000-0000-0000-0000-000000001507', 'PICKUP_REQUEST', '00000000-0000-0000-0000-000000001026', 24000,
     '00000000-0000-0000-0000-000000000003', 480000, 5, '[DEMO] Phí thu gom carton đã hoàn tất.', NOW() - INTERVAL '10 days')
ON CONFLICT DO NOTHING;

-- Hóa đơn kỳ hiện tại khớp tổng phí đã ghi nhận ở các giao dịch demo phía trên.
INSERT INTO platform_invoices (id, payer_id, period_year, period_month, total_fee_amount, status, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001511', '00000000-0000-0000-0000-000000000003',
     EXTRACT(YEAR FROM CURRENT_DATE)::int,
     EXTRACT(MONTH FROM CURRENT_DATE)::int,
     COALESCE((SELECT SUM(fee_amount) FROM platform_transactions
               WHERE payer_id = '00000000-0000-0000-0000-000000000003'
                 AND created_at >= date_trunc('month', NOW())
                 AND created_at < date_trunc('month', NOW()) + INTERVAL '1 month'), 0), 'PENDING', NOW()),
    ('00000000-0000-0000-0000-000000001512', '00000000-0000-0000-0000-000000000006',
     EXTRACT(YEAR FROM CURRENT_DATE)::int,
     EXTRACT(MONTH FROM CURRENT_DATE)::int,
     COALESCE((SELECT SUM(fee_amount) FROM platform_transactions
               WHERE payer_id = '00000000-0000-0000-0000-000000000006'
                 AND created_at >= date_trunc('month', NOW())
                 AND created_at < date_trunc('month', NOW()) + INTERVAL '1 month'), 0), 'PENDING', NOW())
ON CONFLICT DO NOTHING;

INSERT INTO notifications (id, user_id, title, message, is_read, created_at, pickup_request_id)
VALUES
    ('00000000-0000-0000-0000-000000001311', '00000000-0000-0000-0000-000000000003', 'Đơn chờ thanh toán', '[DEMO] Đơn 1004 đang chờ chủ kho chuyển tiền.', FALSE, NOW(), '00000000-0000-0000-0000-000000001004'),
    ('00000000-0000-0000-0000-000000001312', '00000000-0000-0000-0000-000000000009', 'Chờ xác nhận đã nhận tiền', '[DEMO] Chủ kho đã gửi tiền cho đơn 1023.', FALSE, NOW(), '00000000-0000-0000-0000-000000001023'),
    ('00000000-0000-0000-0000-000000001313', '00000000-0000-0000-0000-00000000000c', 'Đơn chờ thu gom', '[DEMO] Seller mới đã gửi yêu cầu tại Ngũ Hành Sơn.', FALSE, NOW(), '00000000-0000-0000-0000-000000001027')
ON CONFLICT DO NOTHING;

INSERT INTO audit_logs (id, user_id, action, entity_name, entity_id, new_data, created_at)
VALUES
    ('00000000-0000-0000-0000-000000001521', '00000000-0000-0000-0000-000000000001', 'CREATE', 'MarketPrice',
     '00000000-0000-0000-0000-000000000060', '{"materialType":"PET","demo":true}', NOW() - INTERVAL '2 days'),
    ('00000000-0000-0000-0000-000000001522', '00000000-0000-0000-0000-000000000001', 'GENERATE', 'PlatformInvoice',
     '00000000-0000-0000-0000-000000001511', '{"demo":true}', NOW() - INTERVAL '5 days')
ON CONFLICT DO NOTHING;

COMMIT;
