-- Dữ liệu demo local của Ngô Sỹ Giá: dùng Retrack_TV2_dev sau migration EF và khởi tạo dữ liệu mẫu.
-- Không chuyển tiền hoặc liên hệ dịch vụ bên ngoài.
DO $$ BEGIN
  IF current_database() <> 'Retrack_TV2_dev' THEN
    RAISE EXCEPTION 'Dữ liệu kiểm thử chỉ được dùng trên Retrack_TV2_dev';
  END IF;
END $$;

INSERT INTO pickup_requests
  (id, seller_id, target_depot_id, accepted_collector_id, description, address,
   gross_amount, platform_fee_percentage, platform_fee_amount, net_amount, status, created_at, updated_at)
VALUES
  ('22222222-2222-2222-2222-222222222201', '00000000-0000-0000-0000-000000000002',
   '00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000004',
   'Kiểm thử E2E Ngô Sỹ Giá — không chuyển tiền thật', 'Địa chỉ kiểm thử',
   100000, 5, 5000, 95000, 'AWAITING_PAYMENT', now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO pickup_request_items (id, pickup_request_id, material_type, weight_kg, price_per_kg, sub_total)
VALUES ('22222222-2222-2222-2222-222222222202', '22222222-2222-2222-2222-222222222201', 'Nhựa PET', 10, 10000, 100000)
ON CONFLICT (id) DO NOTHING;
