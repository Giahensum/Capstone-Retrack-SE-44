-- Hóa đơn giả để Ngô Sỹ Giá kiểm thử giao diện local; không phải công nợ hoặc thanh toán thật.
DO $$ BEGIN
  IF current_database() <> 'Retrack_TV2_dev' THEN
    RAISE EXCEPTION 'Dữ liệu kiểm thử chỉ được dùng trên Retrack_TV2_dev';
  END IF;
END $$;
INSERT INTO platform_fee_invoices (id, owner_id, period_start, amount, status, created_at)
VALUES ('22222222-2222-2222-2222-222222222203',
        '00000000-0000-0000-0000-000000000003', DATE '2026-08-01', 5000, 'UNPAID', now())
ON CONFLICT DO NOTHING;
