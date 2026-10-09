-- Chỉ áp dụng cho Retrack_TV2_dev của TV2. Không tạo/xóa kho, nhà máy, lô, chuyến hay giao dịch.
-- Chỉ đổi địa chỉ và tọa độ các bản ghi demo cố định còn giữ đúng địa chỉ TP.HCM cũ.
-- Các điểm tọa độ là vị trí tham khảo để thử bản đồ/bán kính, không phải cơ sở kinh doanh thật.
BEGIN;
DO $seed$
DECLARE
    item record;
    old_owner uuid := '00000000-0000-0000-0000-000000000003';
    factory_owner uuid := '00000000-0000-0000-0000-000000000006';
BEGIN
    IF current_database() <> 'Retrack_TV2_dev' THEN
        RAISE EXCEPTION 'Script chỉ cho phép database Retrack_TV2_dev, hiện tại: %', current_database();
    END IF;

    FOR item IN SELECT * FROM (VALUES
        ('00000000-0000-0000-0000-000000000010'::uuid, 'Kho Vựa Phế Liệu Minh Bình', '123 Đường Lý Thường Kiệt, Phường 7, Quận Tân Bình, TP.HCM', 'Kho Vựa Phế Liệu Minh Bình', 'Khu vực X7P4+XF9, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)', 15.9874125::numeric, 108.2562344::numeric),
        ('00000000-0000-0000-0000-000000000101'::uuid, 'Vựa Phế Liệu Bình Thạnh', '150 Điện Biên Phủ, Phường 25, Bình Thạnh, TP.HCM', 'Vựa Phế Liệu FPT City', 'Khu vực quảng trường FPT City, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)', 15.9814400::numeric, 108.2610600::numeric),
        ('00000000-0000-0000-0000-000000000102'::uuid, 'Kho Thu Mua Phế Liệu Quận 10', '212 Lý Thái Tổ, Phường 1, Quận 10, TP.HCM', 'Kho Thu Mua Hòa Hải', 'Khu đô thị FPT, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)', 15.9832468::numeric, 108.2520905::numeric),
        ('00000000-0000-0000-0000-000000000103'::uuid, 'Điểm Thu Gom Tân Bình', '78 Cộng Hòa, Phường 4, Tân Bình, TP.HCM', 'Điểm Thu Gom Ngũ Hành Sơn', 'Khu vực đường Huyền Trân Công Chúa, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)', 16.0043044::numeric, 108.2635270::numeric),
        ('00000000-0000-0000-0000-000000000104'::uuid, 'Kho Phế Liệu Lớn Gò Vấp', '152 Quang Trung, Phường 10, Gò Vấp, TP.HCM', 'Kho Phế Liệu Mỹ An', 'Khu vực Mỹ An, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)', 16.0250950::numeric, 108.2595335::numeric)
    ) AS demo(id, old_name, old_address, new_name, new_address, new_lat, new_lng)
    LOOP
        PERFORM id FROM depots WHERE id = item.id AND owner_id = old_owner FOR UPDATE;
        IF NOT FOUND THEN RAISE EXCEPTION 'Thiếu kho demo % hoặc sai chủ; không thay dữ liệu khác.', item.id; END IF;
        IF EXISTS (SELECT 1 FROM depots WHERE id = item.id AND name = item.old_name AND address = item.old_address) THEN
            UPDATE depots SET name = item.new_name, address = item.new_address,
                latitude = item.new_lat, longitude = item.new_lng
            WHERE id = item.id;
        ELSIF NOT EXISTS (SELECT 1 FROM depots WHERE id = item.id AND name = item.new_name
            AND address = item.new_address AND latitude = item.new_lat AND longitude = item.new_lng) THEN
            RAISE EXCEPTION 'Kho % đã được sửa thủ công; không ghi đè.', item.id;
        END IF;
    END LOOP;

    PERFORM id FROM factories
    WHERE id = '00000000-0000-0000-0000-000000000020' AND owner_id = factory_owner FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Thiếu nhà máy demo hoặc sai chủ; không thay dữ liệu khác.'; END IF;
    IF EXISTS (SELECT 1 FROM factories WHERE id = '00000000-0000-0000-0000-000000000020'
        AND name = 'Eco Plastics Vietnam' AND address = '45 Khu Công Nghiệp Tân Bình, TP.HCM') THEN
        UPDATE factories SET address = 'Khu đô thị FPT, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)',
            latitude = 15.9832468, longitude = 108.2520905
        WHERE id = '00000000-0000-0000-0000-000000000020';
    ELSIF NOT EXISTS (SELECT 1 FROM factories WHERE id = '00000000-0000-0000-0000-000000000020'
        AND name = 'Eco Plastics Vietnam'
        AND address = 'Khu đô thị FPT, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)'
        AND latitude = 15.9832468 AND longitude = 108.2520905) THEN
        RAISE EXCEPTION 'Nhà máy demo đã được sửa thủ công; không ghi đè.';
    END IF;
END
$seed$;
COMMIT;

SELECT id, name, address, latitude, longitude FROM depots
WHERE owner_id = '00000000-0000-0000-0000-000000000003'
  AND id IN ('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000101',
             '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000103',
             '00000000-0000-0000-0000-000000000104')
ORDER BY id;
SELECT id, name, address, latitude, longitude FROM factories
WHERE id = '00000000-0000-0000-0000-000000000020';
