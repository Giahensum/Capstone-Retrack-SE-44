-- Hóa đơn giả để Ngô Sỹ Giá kiểm thử giao diện local; không phải công nợ hoặc thanh toán thật.
DO $$ BEGIN
  IF current_database() <> 'Retrack_TV2_dev' THEN
    RAISE EXCEPTION 'Dữ liệu kiểm thử chỉ được dùng trên Retrack_TV2_dev';
  END IF;
END $$;
INSERT INTO platform_invoices (id, payer_id, period_year, period_month, total_fee_amount, status, created_at)
VALUES ('22222222-2222-2222-2222-222222222203',
        '00000000-0000-0000-0000-000000000003', 2026, 8, 5000, 'PENDING', now())
ON CONFLICT DO NOTHING;
