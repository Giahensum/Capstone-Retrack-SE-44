# Khởi tạo cơ sở dữ liệu ReTrack

## Máy mới: một lệnh dựng schema và nạp seed

Cài PostgreSQL và `psql`, clone repo rồi mở terminal tại thư mục gốc. Chạy:

```powershell
psql -v dbname=Retrack_Team -U postgres -d postgres -f db/bootstrap.psql
```

Thay `Retrack_Team` bằng tên database cần dùng. Lệnh sẽ tạo database nếu chưa có, kết nối vào database đó, dựng toàn bộ schema rồi nạp dữ liệu demo. Tài khoản PostgreSQL cần quyền `CREATEDB` và quyền tạo extension. Nếu database đã có bảng trong schema `public`, script chủ động dừng và không sửa dữ liệu.

Sau đó cấu hình backend trỏ đúng database vừa tạo bằng biến môi trường `ConnectionStrings__DefaultConnection` (hoặc `.env` local). Ví dụ định dạng, tự điền tài khoản/mật khẩu PostgreSQL của máy mình:

```text
Host=localhost;Port=5432;Database=Retrack_Team;Username=<postgres-user>;Password=<local-password>
```

Không sửa `appsettings.json` riêng của thành viên để khớp máy người khác. API không tự chạy EF migration; bật/tắt `Database__Initialize` không thay thế bước SQL này.

## Nguồn schema và dữ liệu

- `depot ower/retrack-system.sql` là **nguồn schema DB-first duy nhất**. Bao gồm các bảng/model dùng chung của Auth, Seller, Depot Owner, Employee, Driver, Factory và Admin, cùng các cột đang được code sử dụng như liên kết thông báo, ảnh lô, thanh toán người dùng, KCS/QC và nhật ký giao nhận.
- `retrack-demo-seed.sql` là dữ liệu demo phát triển tách riêng. Có tài khoản các role, kho và nhà máy Đà Nẵng, nhu cầu vật liệu, đơn Seller/Employee, một khoản Seller chờ chi, tồn PET mẫu, lời mời lô đầu tiên chờ Factory và bảng giá tham khảo. Dữ liệu seed gắn nhãn `[DEMO]`; không đại diện giao dịch thật.
- `bootstrap.psql` là điểm chạy một lần, tự gọi đúng hai file trên. Không gộp seed vào schema để người dùng có thể tạo lại database sạch mà không phải sửa DDL.
- `employee/02_employee_demo.sql` và `driver/02_driver_demo.sql`, `driver/04_driver_danang_local_demo.sql` là fixture kiểm thử mở rộng tùy chọn; không cần chạy để đăng nhập và thử luồng cơ bản. Đọc README của từng role trước khi chạy fixture bổ sung.

## Quy tắc khi thành viên thêm cột/bảng

Mọi thay đổi schema của bất kỳ role nào phải được báo trong PR và cập nhật vào **cùng** `depot ower/retrack-system.sql`. Không tạo một file `CREATE TABLE` song song làm nguồn schema thứ hai. Với database đã có dữ liệu, kèm script nâng cấp idempotent, giải thích thứ tự chạy và kiểm thử nâng cấp trên bản sao; không chạy bootstrap lên database hiện hữu. Review cả model EF, service/controller đang truy vấn, seed và schema trước khi merge để tránh thiếu cột khi thành viên khác pull code.

Các file `factory/upgrade_existing_factory_schema.sql`, `employee/01_employee_setup.sql`, `employee/03_notification_pickup_link.sql`, `driver/01_driver_setup.sql`, `driver/03_driver_delivery.sql` chỉ dành cho nâng cấp một database cũ cụ thể; chúng không thay thế nguồn schema cho máy mới. Không chạy nâng cấp cũ sau bootstrap nếu không có yêu cầu tương thích rõ ràng.

Mật khẩu tài khoản demo chỉ được lưu dưới dạng BCrypt hash. Đây là thông tin phát triển; không tái sử dụng ngoài môi trường demo, không đưa secret Cloudinary/PayOS/JWT/PostgreSQL vào SQL hoặc repo.
