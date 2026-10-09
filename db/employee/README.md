# SQL cho Depot Employee

Máy mới chỉ cần chạy `db/bootstrap.psql` một lần; schema Employee và các bảng chung đã nằm trong nguồn DB-first `db/depot ower/retrack-system.sql`. Các script bên dưới là fixture kiểm thử mở rộng hoặc bản vá cho database cũ, không chạy lại trên máy mới nếu không có nhu cầu cụ thể.

Khi cần chạy fixture bằng pgAdmin, chọn đúng database backend. Đây là script bổ sung trên schema ReTrack hiện có, không phải schema song song.

## Thứ tự chạy

**Cập nhật chuông thông báo:** DB đã dùng từ trước cần chạy thêm `03_notification_pickup_link.sql` trước khi khởi động backend mới. Chỉ thêm liên kết thông báo → đơn, không cần chạy lại seed. Áp dụng cả máy dùng Driver vì Notification là model chung.

1. `01_employee_setup.sql`: bổ sung cột hồ sơ kho cần cho đăng nhập, bảng check-in, bảng nhật ký gửi cân/bàn giao; đặt cấu hình phí chung thành 5%. Không đổi phí đơn cũ. Chỉ cần chạy một lần sau khi cập nhật code; có thể chạy lại.
2. `02_employee_demo.sql`: tạo đủ 13 đơn demo Đà Nẵng, gồm điểm check-in tại chỗ; mỗi đơn mới có phí 5%. Cần sẵn tài khoản `employee@retrack.vn`, `seller@retrack.vn` và liên kết nhân viên–kho đang hoạt động.

Mở từng file và chạy toàn bộ bằng F5. Không chỉ chọn riêng đoạn INSERT. Không cần xóa bảng.

## Làm lại dữ liệu demo

Trong `02_employee_demo.sql`, đổi:

```sql
SET LOCAL retrack.reset_employee_demo = 'false';
```

thành `'true'`, dừng backend, rồi chạy toàn bộ file trên DB phát triển cá nhân. Reset và seed nằm trong cùng transaction; nếu seed lỗi thì phần xóa cũng rollback. Sau đó đổi lại `'false'` để lần sau không reset nhầm.

Reset chỉ xóa 13 UUID demo có đúng nhãn: pickup_requests và các dòng con pickup_request_items, pickup_checkins, employee_collection_events; xóa đánh giá liên quan và thông báo có UUID rõ ràng. Không xóa users, depots, depot_staffs hoặc đơn ngoài bộ demo. Script từ chối reset đơn đã phát sinh thanh toán/tồn kho; không tự xóa platform_transactions. Các ảnh Cloudinary cũ không bị xóa bởi SQL.

## Hợp nhất file cũ

- Các script check-in, dashboard, bổ sung cột depot và cấu hình phí đã hợp nhất vào `01_employee_setup.sql`.
- Script seed và reset demo đã hợp nhất vào `02_employee_demo.sql`, với reset mặc định tắt.
- Các kho demo dùng chung được tạo bằng `DataSeeder` và `Program.cs` khi bật `Database__Initialize` ở Development. Bản SQL kho TP.HCM cũ đã chuyển sang lưu trữ local trong `.ai-context` và không còn dùng để seed.
- Schema đầy đủ của hệ thống vẫn nằm ở `db/depot ower/retrack-system.sql`.

Các script đã được rà soát cấu trúc và đường dẫn; chưa thực thi trên database của bạn.
