# SQL Driver

Chạy trong pgAdmin trên **database phát triển cá nhân** đang kết nối backend, theo thứ tự:

1. `01_driver_setup.sql`: thêm liên kết chuyến cho thông báo. Đây là file `db/drv_job_pool.sql` đã chuyển vào thư mục này.
2. `02_driver_demo.sql`: chạy toàn bộ để tạo 3 lô `DRV-DEMO-01/02/03`, PET 100/150/200 kg và 3 chuyến chờ nhận. Giữ nguyên tài khoản, mật khẩu, địa chỉ và các dữ liệu khác.

Điều kiện: có `driver@retrack.vn` cùng liên kết kho đang hoạt động, Seller và Factory đang hoạt động. Có thể sửa `driver_email` và `chosen_factory_id` ở đầu file. Để Factory NULL thì tự chọn nhà máy nhận PET, có tọa độ, không bị chặn, ưu tiên gần kho. Nếu không đủ điều kiện, toàn transaction rollback và báo lý do; không tạo tài khoản hoặc địa điểm giả để lấp dữ liệu thiếu.

Để test bản đồ Đà Nẵng, kiểm tra kho và nhà máy trong DB có địa chỉ/tọa độ tại Đà Nẵng. Script giữ nguyên điểm hiện có; không đổi kho TP.HCM sang Đà Nẵng. Ba lô dùng cùng kho/nhà máy nên có thể cùng tuyến đường, đây không phải lỗi.

Seed thêm 3 đơn nguồn DONE giả lập cùng 3 dòng PET, tổng 450 kg, tương ứng 450 kg giữ cho các lô. Các đơn này không có nhân viên thu gom, giá/tiền bằng 0, không tạo giao dịch thanh toán. Chỉ là fixture kiểm thử Driver, có thể ảnh hưởng số đơn hoàn tất/thống kê Seller và kho; không dùng trên production hoặc database dùng chung. Không coi đây là mẫu nghiệp vụ thanh toán thực tế.

Chạy lại không nhân đôi hoặc reset trạng thái chuyến đã nhận. Không cần xóa bảng. Nếu bộ fixture bị xóa một phần, script báo lỗi thay vì ghi đè hoặc âm thầm tái sử dụng nguồn hàng. Không reset lô đã vận chuyển/QC/thanh toán bằng cách xóa tùy tiện.

Sau khi seed lần đầu, truy vấn cuối file trả 3 lô ACCEPTED, chuyến PENDING và driver_id NULL. Mở app Driver → Chuyến xe → làm mới → xem/nhận chuyến; có ba thông báo demo. Backend cần đang chạy code mới. Không cần cài lại APK.

Đã rà soát `db`, migration EF và DataSeeder: migration tạo schema/seed system_configs; chưa có bộ seed lô/chuyến Driver tương đương. Script mới đã được kiểm tra tĩnh, chưa thực thi trên database của bạn.
