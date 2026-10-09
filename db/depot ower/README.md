# Database và dữ liệu mẫu Depot Owner

- `retrack-system.sql`: schema chung cho **database mới, rỗng**. Ứng dụng không tự chạy EF migration. Đọc `README.md` ở gốc repo trước khi áp dụng vào database đã có dữ liệu.
- `seed-depot-danang-existing-demo.sql`: bản SQL **chỉ cập nhật địa chỉ/tọa độ** năm kho demo và một nhà máy demo đã tồn tại từ TP.HCM sang Ngũ Hành Sơn, Đà Nẵng. Giữ nguyên ID, chủ sở hữu, đơn, lô và thanh toán. Script chỉ chấp nhận database tên `Retrack_TV2_dev`, đối chiếu ID/tên/địa chỉ cũ và có thể chạy lại. Không dùng trên database chung hoặc production.

Tài khoản demo, kho đầu tiên, nhân sự, nhà máy, nhu cầu mua và giá tham khảo được tạo bởi `backend/Retrack.API/Data/DataSeeder.cs` **chỉ ở Development khi** `Database__Initialize=true` và database đã có schema. Đây là seed C#, chưa có file SQL riêng tương đương; không chạy SQL cập nhật địa chỉ để thay cho bước tạo dữ liệu ban đầu. Đừng bật seed mặc định trên database dùng chung.

Muốn kiểm tra lô xuất có ảnh, cần cấu hình Cloudinary hợp lệ trong `.env` local. Từ bản sửa này, upload thất bại trả lỗi và không tạo lô/giữ tồn; ứng dụng không lưu ảnh mẫu của Cloudinary như ảnh vật liệu.
