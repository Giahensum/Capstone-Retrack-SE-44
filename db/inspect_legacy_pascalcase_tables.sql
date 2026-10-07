-- Chỉ đọc: kiểm kê bảng legacy PascalCase và số dòng trước khi dọn schema.
-- Chạy trên đúng database đang mở trong pgAdmin.

WITH legacy_tables(table_name) AS (
    VALUES
        ('AuditLogs'),
        ('EprCertificates'),
        ('PlatformFeeLogs'),
        ('Invoices'),
        ('WeightTickets'),
        ('WeightVerifications'),
        ('TransportTrackingLogs'),
        ('TransportJobs'),
        ('BatchOrders'),
        ('BatchImages'),
        ('InventoryBatches'),
        ('PickupRequestImages'),
        ('PickupRequestItems'),
        ('PickupRequests'),
        ('Partnerships'),
        ('FactoryDemands'),
        ('MarketPrices'),
        ('Notifications'),
        ('DepotEmployees'),
        ('Drivers'),
        ('Sellers'),
        ('Factories'),
        ('Depots'),
        ('Users')
)
SELECT
    l.table_name,
    CASE WHEN c.oid IS NULL THEN 'absent' ELSE 'exists' END AS table_status,
    CASE WHEN c.oid IS NULL THEN NULL ELSE c.reltuples::bigint END AS estimated_rows,
    CASE WHEN c.oid IS NULL THEN NULL ELSE (
        SELECT count(*)
        FROM information_schema.columns col
        WHERE col.table_schema = 'public'
          AND col.table_name = l.table_name
    ) END AS column_count
FROM legacy_tables l
LEFT JOIN pg_class c
    ON c.oid = to_regclass(format('public.%I', l.table_name))
ORDER BY l.table_name;

-- Ước lượng dòng lấy từ thống kê PostgreSQL; chạy ANALYZE trước nếu cần độ chính xác.
