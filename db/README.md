# Khởi tạo cơ sở dữ liệu ReTrack

## Máy mới: chạy hai file trong pgAdmin

Tạo database trống trong pgAdmin (right-click **Databases → Create → Database**), mở Query Tool của database đó, rồi chạy lần lượt toàn bộ hai file dưới đây bằng F5:

1. `db/bootstrap/01-retrack-system.sql` — dựng toàn bộ schema.
2. `db/bootstrap/02-seed-data.sql` — nạp dữ liệu demo chung cho tất cả role.

Đây là đúng **hai file khởi tạo database mới**. Tạo database trong giao diện pgAdmin là bước chuẩn bị; hai file SQL chạy trong Query Tool theo thứ tự trên. Nếu database đã có bảng thì không chạy schema/seed như DB mới vì dữ liệu sẽ được giữ nguyên và script không phải migration.

Sau đó cấu hình backend trỏ đúng database vừa tạo bằng biến môi trường `ConnectionStrings__DefaultConnection` (hoặc `.env` local). Ví dụ định dạng, tự điền tài khoản/mật khẩu PostgreSQL của máy mình:

```text
Host=localhost;Port=5432;Database=Retrack_Team;Username=<postgres-user>;Password=<local-password>
```

Không sửa `appsettings.json` riêng của thành viên để khớp máy người khác. API không tự chạy EF migration; bật/tắt `Database__Initialize` không thay thế bước SQL này.

## Nguồn schema và dữ liệu

- `bootstrap/01-retrack-system.sql` là **nguồn schema DB-first duy nhất** cho Auth, Seller, Depot Owner, Employee, Driver, Factory và Admin.
- `bootstrap/02-seed-data.sql` là seed chung: 13 tài khoản thuộc 6 role, 3 điểm kho, 3 nhà máy, 6 nhu cầu theo vật liệu, 16 yêu cầu thu gom ở nhiều trạng thái, lượng tồn PET/nhôm/carton, 10 lô từ chờ duyệt tới KCS/quyết toán, 6 chuyến, đánh giá, giao dịch phí, hóa đơn, thông báo, nhật ký Admin và bảng giá demo. Tọa độ/địa chỉ mẫu trải từ Ngũ Hành Sơn, Hải Châu tới Cẩm Lệ. Dữ liệu có nhãn `[DEMO]`, không phải nghiệp vụ thật.
- Seed chạy lại không nhân đôi các bản ghi fixture; không sửa tài khoản có sẵn khi ID/email trùng và có kiểm tra dừng nếu dữ liệu tài khoản xung đột.
- Các tài khoản phụ thêm để kiểm tra phân quyền/danh sách là `seller4@retrack.vn`, `employee2@retrack.vn` và `driver2@retrack.vn`. Mật khẩu tương ứng giống tài khoản demo chính cùng role. Hóa đơn mẫu ở trạng thái `PENDING`, chưa chuyển tiền thật; số tiền bằng tổng giao dịch demo của người trả phí trong tháng chạy seed. Dữ liệu KCS mẫu không có ảnh/chứng từ giả; có thể tải lên bằng giao diện khi kiểm thử.
- Các file SQL trong `employee/`, `driver/` và `factory/` là bản nâng cấp DB cũ hoặc fixture chuyên biệt; không phải file cần chạy khi setup database mới. Đọc README theo role trước khi dùng chúng. Hai script `cleanup_legacy_pascalcase_tables.sql` và `inspect_legacy_pascalcase_tables.sql` chỉ phục vụ kiểm tra/chuyển dữ liệu từ schema PascalCase đời đầu.
- SQL chuyển địa chỉ demo cá nhân sang Đà Nẵng không còn trong repo vì seed dùng chung đã có địa chỉ Đà Nẵng; bản theo dõi cục bộ được lưu trong `.ai-context` và không dùng khi tạo database mới. Thư mục `depot ower/` giữ README riêng cho Depot Owner; hai file SQL bootstrap toàn hệ thống nằm trong `db/bootstrap/`.

## Quy tắc khi thành viên thêm cột/bảng

Mọi thay đổi schema của bất kỳ role nào phải được báo trong PR và cập nhật vào **cùng** `bootstrap/01-retrack-system.sql`. Không tạo một file `CREATE TABLE` song song làm nguồn schema thứ hai. Với database đã có dữ liệu, kèm script nâng cấp idempotent, giải thích thứ tự chạy và kiểm thử nâng cấp trên bản sao; không chạy hai file khởi tạo mới lên database hiện hữu. Review cả model EF, service/controller đang truy vấn, seed và schema trước khi merge để tránh thiếu cột khi thành viên khác pull code.

Các file nâng cấp schema role chỉ dành cho database cũ theo tình huống cụ thể; chúng không thay thế hai file khởi tạo máy mới. Không chạy nâng cấp cũ sau khi đã dựng schema mới nếu không có yêu cầu tương thích rõ ràng.

Mật khẩu tài khoản demo chỉ được lưu dưới dạng BCrypt hash. Đây là thông tin phát triển; không tái sử dụng ngoài môi trường demo, không đưa secret Cloudinary/PayOS/JWT/PostgreSQL vào SQL hoặc repo.
