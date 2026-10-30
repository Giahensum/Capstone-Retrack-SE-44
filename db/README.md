# Database ReTrack — chỉ chạy một file SQL

**File duy nhất: [`bootstrap/retrack-system.sql`](bootstrap/retrack-system.sql).** Gồm schema toàn hệ thống và seed chung cho Admin, Seller, Depot Owner, Employee, Driver, Factory. Không cần chạy thêm SQL theo role.

## Máy mới: chạy thủ công trong pgAdmin

1. Tạo database mới, rỗng, ví dụ `Retrack_DB`.
2. Nhấp phải đúng database → Query Tool → Open File.
3. Mở `db/bootstrap/retrack-system.sql` từ source vừa pull.
4. Bỏ vùng chọn nếu đang chọn một đoạn; nhấn F5 chạy toàn bộ file.
5. Khi thấy COMMIT/Query returned successfully, Refresh Schemas → public → Tables.
6. Trỏ backend tới DB đó bằng `ConnectionStrings__DefaultConnection` trong `backend/Retrack.API/.env` local. Giữ tài khoản/mật khẩu PostgreSQL của máy mình.
7. Giữ `Database__Initialize=false`, rồi chạy backend theo README dự án.

Không cần terminal để dựng DB, không cần file seed thứ hai hoặc `dotnet ef database update`. Backend không tự migrate khi `dotnet run`.

## Dữ liệu demo đã gộp

Mặc định file có `SET LOCAL retrack.seed_demo = 'on';` để nạp:

| Nhóm | Dữ liệu |
|---|---|
| Tài khoản | 26 tài khoản, đủ 6 role; Chủ kho demo Ngô Sỹ Giá. Giữ các email chính admin/seller/depot/employee/driver/factory@retrack.vn và Factory2/3 |
| Kho/nhà máy | 5 kho, 4 nhà máy, 15 liên kết nhân sự; địa chỉ mô phỏng Đà Nẵng |
| Thu gom | 65 đơn nhiều trạng thái, gồm 13 đơn Employee `e0000000-0929-*` và 36 đơn điều phối `ed250000-*` |
| Điều phối | 6 nhân viên, 6 Seller, 6 vùng và ca demo khoảng 20 giờ; đơn hẹn ngày sau chưa được giao từ hôm nay |
| Vận chuyển | 12 chuyến, gồm 3 lô `DRV-DEMO-01/02/03` và 3 lô `DRV-DN-LOCAL-01/02/03`, giữ UUID cũ để mobile tiếp tục test |
| Tồn/chi phí | Phiếu sắt+ghế cùng chi phí; thêm 900 kg PET nguồn demo cho hai bộ lô Driver, giá vốn chưa xác định |
| Liên role | Hợp tác, nhu cầu, lô xuất/cân/KCS, thanh toán/phí/hóa đơn, thông báo, bảng giá và lô thanh lý |

- `employee.dispatch01@retrack.test`…`employee.dispatch06@retrack.test` và Seller dispatch sao chép hash từ tài khoản Employee/Seller nguồn khi tạo lần đầu. `factory.driver-local@retrack.test` sao chép hash Factory nguồn khi tạo lần đầu; không đổi mật khẩu tài khoản đã có.
- Lô Driver tại chỗ dùng điểm `X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng`. Kho và nhà máy cùng tọa độ chỉ để thử camera/giao nhận tại chỗ, không dùng đánh giá thuật toán tuyến đường.
- Dữ liệu chỉ mô phỏng, không phải yêu cầu thu gom/giao hàng/thanh toán thật. Nguồn Driver chưa khai giá vốn không được coi vốn bằng 0 để báo lãi.
- Ca demo hết giờ sau khoảng 20 giờ. Hôm khác Owner tạo ca mới ở Điều phối thu gom; chạy lại file không bật ca STOPPED/FULL hoặc kéo dài ca đã dùng.

## Chỉ lấy schema

Đổi chính dòng đầu file thành `SET LOCAL retrack.seed_demo = 'off';`, rồi chạy toàn bộ. Tất cả bộ demo cùng tuân theo tùy chọn này. Cấu hình hệ thống mặc định vẫn được tạo để API hoạt động; không tạo tài khoản/đơn demo. Không bật seed demo ở production.

## Chạy lại và lỗi thường gặp

Schema và seed nằm trong một transaction. Chạy lại trên DB demo cùng cấu trúc không nhân đôi UUID, reset đơn/chuyến đã xử lý, thay mật khẩu hoặc mở lại ca đã ngừng. Không có tùy chọn reset dữ liệu trong file chung. Dừng backend trước khi chạy lại trên các đơn demo đang dùng.

- `NOTICE ... already exists, skipping`: đối tượng đã có, không phải lỗi.
- Nếu có ERROR, đọc lỗi đầu tiên; chạy `ROLLBACK;` trong cùng Query Tool, sửa nguyên nhân rồi thử lại. Không xóa database để sửa lỗi.
- Xung đột ID/email/role hoặc fixture dở dang sẽ dừng transaction, không ghi đè dữ liệu để ép seed thành công.
- Upload/PayOS/Google/Goong cần cấu hình riêng. SQL không chứa token hoặc mật khẩu kết nối PostgreSQL.

## DB cũ đã có dữ liệu

Pull code không tự sửa PostgreSQL. Sao lưu, kiểm tra `SELECT current_database();` và đối chiếu schema trước khi nâng cấp. Dùng cùng file SQL để chọn khối schema cần thiết, kết thúc trước `-- RETRACK_SCHEMA_END`, chạy trong transaction riêng. Không chọn sang `-- RETRACK_SEED_BEGIN` khi nâng cấp dữ liệu thật.

Marker chính: `RETRACK_DEPOT_STOCK_V1`, `RETRACK_LIQUIDATION_DEPOSIT_V1`, `RETRACK_DEPOT_NOTIFICATIONS_V1`, `RETRACK_EMPLOYEE_DISPATCH_BEGIN/END`. CREATE TABLE IF NOT EXISTS không tự sửa mọi cột/kiểu/bảng legacy; bootstrap không chuyển đổi mọi DB lịch sử. Không áp EF migration chồng lên SQL để thử hết lỗi.

Các SQL cũ đã gộp/xóa khỏi Git. Script kiểm kê/dọn PascalCase lưu local trong `.ai-context`, không chạy kèm bootstrap hoặc tự xóa bảng legacy. Tra lịch sử Git trước commit hợp nhất khi cần đối chiếu. Tệp Factory trên ổ đĩa vẫn phải lưu bền vững và sao lưu cùng DB; SQL không chứa nội dung tệp.

## Khi nhóm thêm bảng/cột hoặc seed

Chỉnh cùng `bootstrap/retrack-system.sql`: schema trước RETRACK_SCHEMA_END, seed giữa RETRACK_SEED_BEGIN/END. Mọi seed phải kiểm retrack.seed_demo, giữ UUID và trạng thái đã dùng. Không thêm bootstrap/seed theo role song song. Giữ các marker mà tests đang dùng; trước push kiểm bootstrap mới, schema-only, chạy lại và rollback khi lỗi.
