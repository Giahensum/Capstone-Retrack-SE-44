# Database và dữ liệu mẫu Depot Owner

- `retrack-system.sql`: file 1/2 — schema chung cho **database mới, rỗng**, mọi role. Chạy toàn bộ trong Query Tool của pgAdmin bằng F5. Ứng dụng không tự chạy EF migration.
- `seed-data.sql`: file 2/2 — seed demo chung nhiều địa điểm và luồng ở Đà Nẵng cho toàn hệ thống. Chạy sau schema bằng F5; có thể chạy lại an toàn với các UUID fixture.
- `seed-depot-danang-existing-demo.sql`: bản SQL **chỉ cập nhật địa chỉ/tọa độ** năm kho demo và một nhà máy demo đã tồn tại từ TP.HCM sang Ngũ Hành Sơn, Đà Nẵng. Giữ nguyên ID, chủ sở hữu, đơn, lô và thanh toán. Script chỉ chấp nhận database tên `Retrack_TV2_dev`, đối chiếu ID/tên/địa chỉ cũ và có thể chạy lại. Không dùng trên database chung hoặc production.

`seed-data.sql` là seed chuẩn cho máy mới. `DataSeeder.cs` vẫn có thể thêm bộ dữ liệu cơ bản khi Development và `Database__Initialize=true`, nhưng bật tùy chọn này không thay thế việc tạo schema DB-first. Đừng bật seed mặc định trên database dùng chung.

Muốn kiểm tra lô xuất có ảnh, cần cấu hình Cloudinary hợp lệ trong `.env` local. Từ bản sửa này, upload thất bại trả lỗi và không tạo lô/giữ tồn; ứng dụng không lưu ảnh mẫu của Cloudinary như ảnh vật liệu.
