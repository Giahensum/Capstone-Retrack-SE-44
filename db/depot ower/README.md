# Database và dữ liệu mẫu Depot Owner

- Chỉ chạy một file `db/bootstrap/retrack-system.sql` cho **database mới, rỗng** của mọi role: mở file trong Query Tool pgAdmin và nhấn F5 chạy toàn bộ. File gồm schema và seed demo Đà Nẵng, có thể tắt seed bằng `retrack.seed_demo='off'`. Ứng dụng không tự chạy EF migration.
- Không còn SQL riêng cho Depot Owner trong thư mục này; schema và seed nằm chung trong file bootstrap. Dữ liệu thử nghiệm chỉ dành cho database cá nhân được lưu ngoài Git trong `.ai-context`.

Phần seed trong file bootstrap là nguồn demo dùng chung cho máy mới. `DataSeeder.cs` vẫn có thể thêm dữ liệu cơ bản khi Development và `Database__Initialize=true`, nhưng không thay thế bước tạo schema. Nếu đã chạy bootstrap có seed thì giữ tùy chọn này là `false`.

Muốn kiểm tra lô xuất có ảnh, cần cấu hình Cloudinary hợp lệ trong `.env` local. Từ bản sửa này, upload thất bại trả lỗi và không tạo lô/giữ tồn; ứng dụng không lưu ảnh mẫu của Cloudinary như ảnh vật liệu.
