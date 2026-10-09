-- Dữ liệu demo dùng cục bộ/phát triển, không dùng production.
-- Máy mới: chạy db/bootstrap/01-retrack-system.sql trước; mở file này trong Query Tool pgAdmin, nhấn F5.
-- Có thể chạy lại; các UUID fixture đã có sẽ được giữ nguyên.
-- Tài khoản demo được tạo khi email/ID chưa tồn tại; mật khẩu chỉ lưu dạng BCrypt.
-- Không dùng thông tin demo cho môi trường thật.

BEGIN;

-- Tài khoản đăng nhập cho Admin, ba Seller, Depot Owner, Employee, Driver và ba Factory.
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
    ('00000000-0000-0000-0000-00000000000a', 'seller3@retrack.vn',  '$2a$11$J/edu1SVelH8SrExp6JGNuuWmaRMULzpLjFhA.1/WcMzB2.O8Hsom', 'SELLER',         'Phạm Minh Khoa',        '0911111113', TRUE, NOW(), NOW())
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
            ('00000000-0000-0000-0000-00000000000a'::uuid, 'seller3@retrack.vn', 'SELLER')
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
    ('00000000-0000-0000-0000-000000000035', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000005', 'DRIVER', TRUE)
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
    ('00000000-0000-0000-0000-000000000042', '00000000-0000-0000-0000-000000000022', 'CARDBOARD', 4000, 2000, 4000, NOW() + INTERVAL '30 days', TRUE, 'Nhu cầu demo bìa carton.', NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Một yêu cầu mới, một đã nhận, một chờ chi Seller và một đã hoàn tất để tạo tồn PET demo.
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
     NOW() - INTERVAL '5 days', 1680000, 5, 84000, 1596000, 'DONE', NOW() - INTERVAL '6 days', NOW() - INTERVAL '5 days')
ON CONFLICT DO NOTHING;

INSERT INTO pickup_request_items (id, pickup_request_id, material_type, weight_kg, price_per_kg, sub_total)
VALUES
    ('00000000-0000-0000-0000-000000001011', '00000000-0000-0000-0000-000000001003', 'PET', 200, 10000, 2000000),
    ('00000000-0000-0000-0000-000000001012', '00000000-0000-0000-0000-000000001004', 'PET', 50, 10000, 500000),
    ('00000000-0000-0000-0000-000000001013', '00000000-0000-0000-0000-000000001008', 'CARDBOARD', 80, 4000, 320000),
    ('00000000-0000-0000-0000-000000001014', '00000000-0000-0000-0000-000000001009', 'ALUMINUM', 60, 28000, 1680000)
ON CONFLICT DO NOTHING;

-- Một lời mời trực tiếp chờ nhà máy quyết định lô đầu tiên; không seed APPROVED.
INSERT INTO factory_depot_partnerships (id, depot_id, factory_id, status, created_at, updated_at)
VALUES
    ('00000000-0000-0000-0000-000000000050', '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000020', 'PENDING', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000051', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000021', 'APPROVED', NOW(), NOW()),
    ('00000000-0000-0000-0000-000000000052', '00000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000022', 'PENDING', NOW(), NOW())
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

COMMIT;
