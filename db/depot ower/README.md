# Database và dữ liệu mẫu Depot Owner

- Bộ SQL bootstrap DB-first của toàn hệ thống được đặt tại `db/bootstrap/`: `01-retrack-system.sql` (schema) và `02-seed-data.sql` (seed). Đây là đúng hai file cần chạy cho **database mới, rỗng**, mọi role; chạy lần lượt trong Query Tool pgAdmin bằng F5. Ứng dụng không tự chạy EF migration.
- Không còn SQL riêng cho Depot Owner trong thư mục này: schema và seed đã nằm trong hai file bootstrap dùng chung. Dữ liệu thử nghiệm chỉ dành cho database cá nhân được lưu ngoài Git trong `.ai-context`.

`seed-data.sql` là seed chuẩn cho máy mới. `DataSeeder.cs` vẫn có thể thêm bộ dữ liệu cơ bản khi Development và `Database__Initialize=true`, nhưng bật tùy chọn này không thay thế việc tạo schema DB-first. Đừng bật seed mặc định trên database dùng chung.

Muốn kiểm tra lô xuất có ảnh, cần cấu hình Cloudinary hợp lệ trong `.env` local. Từ bản sửa này, upload thất bại trả lỗi và không tạo lô/giữ tồn; ứng dụng không lưu ảnh mẫu của Cloudinary như ảnh vật liệu.
