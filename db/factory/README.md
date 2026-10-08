# Nâng cấp schema Factory trên database đã có dữ liệu

API hiện theo DB-first và không tự chạy EF migration. File `upgrade_existing_factory_schema.sql` bổ sung các cột Auth/Factory còn thiếu và bảng `notifications` dùng chung cho lời mời vận chuyển. Script không xóa dữ liệu và có thể chạy lại.

Trước khi áp dụng: sao lưu database, tạo bản sao, chạy script trên bản sao, so sánh số dòng và thử đăng nhập Factory, đọc nhu cầu/đơn, nhận lô và quyết toán. Sau đó kiểm tra `SELECT current_database()` trên kết nối thực tế trước khi chạy script trên database cần nâng cấp. Không chạy script này đồng thời với EF migration hoặc SQL tạo schema cho database rỗng.

Tệp Factory mới được lưu dưới `backend/Retrack.API/uploads/factory` và Git bỏ qua thư mục này. Khi triển khai, cấu hình thư mục đó trên ổ lưu trữ bền vững và sao lưu cùng database; URL trong DB chỉ trỏ tới tệp trên máy chủ. Tệp cũ dạng data URL vẫn đọc được nhưng không được tạo thêm từ giao diện mới.
