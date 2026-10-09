# SQL Driver

Máy mới chỉ cần chạy `db/bootstrap.psql` một lần; schema Driver và các bảng chung đã nằm trong nguồn DB-first `db/depot ower/retrack-system.sql`. File bên dưới là fixture kiểm thử mở rộng hoặc bản vá cho database cũ, không chạy lại trên máy mới nếu không có nhu cầu cụ thể.

Khi cần chạy fixture bằng pgAdmin trên **database phát triển cá nhân** đang kết nối backend, dùng theo thứ tự:

1. `01_driver_setup.sql`: thêm liên kết chuyến cho thông báo. Đây là file `db/drv_job_pool.sql` đã chuyển vào thư mục này.
2. `02_driver_demo.sql`: chạy toàn bộ để tạo 3 lô `DRV-DEMO-01/02/03`, PET 100/150/200 kg và 3 chuyến chờ nhận. Giữ nguyên tài khoản, mật khẩu, địa chỉ và các dữ liệu khác.
3. `03_driver_delivery.sql`: thêm nhật ký bằng chứng/thao tác UC-64..67. Bắt buộc chạy trước backend nhánh `feature/drv-checkin-delivery`, kể cả đã có 3 lô demo.
4. `04_driver_danang_local_demo.sql` (tùy chọn): bổ sung 3 lô `DRV-DN-LOCAL-01/02/03`, một kho và một nhà máy giả lập **cùng điểm test Employee** X7P4+XF9 (`15.9874125, 108.256234375`). Nếu đơn Employee tại chỗ đã có, đọc tọa độ hiện tại của đơn đó. Không cần chạy lại file 02 hay xóa dữ liệu cũ.

File 04 dùng tài xế `driver@retrack.vn` hiện có, thêm một liên kết với kho demo thuộc chủ kho của tài xế; không đổi membership cũ. Tạo Factory demo riêng `factory.driver-local@retrack.test`; mật khẩu ban đầu lấy từ hash của tài khoản Factory hoạt động (ưu tiên `factory@retrack.vn`, email nguồn được in bằng NOTICE). Không thay mật khẩu tài khoản cũ. Thêm dữ liệu vào `users`, `depots`, `depot_staffs`, `factories`, `inventory_batches`, `transport_jobs`, `notifications`. Chỉ đọc tọa độ Employee, không tạo/sửa pickup. Chạy lại giữ nguyên trạng thái và bằng chứng đã test.

Để test tại chỗ: làm mới Chuyến xe → chọn mã `DRV-DN-LOCAL-*` → nhận chuyến → chụp ảnh + GPS → xác nhận lấy hàng → khởi hành → chụp ảnh + GPS mới → xác nhận giao hàng. Nút xác nhận bị khóa khi thiếu ảnh, thiếu GPS hoặc đang xử lý; đứng gần địa điểm nhưng chưa chụp ảnh vẫn không bấm được. Sai số GPS phải ≤50 m và khoảng cách ≤200 m. Kho và nhà máy cùng tọa độ để thử bàn giao, nên bộ này không dùng kiểm thử tuyến đường Goong (có thể không có tuyến vì điểm đi/đến trùng nhau).

Điều kiện: có `driver@retrack.vn` cùng liên kết kho đang hoạt động và Factory đang hoạt động. Có thể sửa `driver_email` và `chosen_factory_id` ở đầu file. Để Factory NULL thì tự chọn nhà máy nhận PET, có tọa độ, không bị chặn, ưu tiên gần kho. Nếu không đủ điều kiện, toàn transaction rollback và báo lý do; không tạo tài khoản hoặc địa điểm giả để lấp dữ liệu thiếu.

Để test bản đồ Đà Nẵng, kiểm tra kho và nhà máy trong DB có địa chỉ/tọa độ tại Đà Nẵng. Script giữ nguyên điểm hiện có; không đổi kho TP.HCM sang Đà Nẵng. Ba lô dùng cùng kho/nhà máy nên có thể cùng tuyến đường, đây không phải lỗi.

Seed chỉ thêm `inventory_batches`, `transport_jobs`, `notifications`; không tạo đơn Seller/Employee. Đây là fixture riêng để test Driver, không dùng để kiểm chứng cân đối tồn kho đầu vào của Depot (vốn tổng hợp từ đơn thu gom DONE).

Sau check-in sẽ có bản ghi `driver_delivery_events` liên kết chuyến và tài xế. Không xóa bảng/chuyến đã có bằng chứng; khóa ngoại RESTRICT giữ lịch sử bàn giao. Dùng chuyến demo mới khi cần test lại.

Chạy lại không nhân đôi hoặc reset trạng thái chuyến đã nhận. Không cần xóa bảng. Nếu bộ fixture bị xóa một phần, script báo lỗi thay vì ghi đè hoặc âm thầm tái sử dụng nguồn hàng. Không reset lô đã vận chuyển/QC/thanh toán bằng cách xóa tùy tiện.

Sau khi seed lần đầu, truy vấn cuối file trả 3 lô ACCEPTED, chuyến PENDING và driver_id NULL. Mở app Driver → Chuyến xe → làm mới → xem/nhận chuyến; có ba thông báo demo. Backend cần đang chạy code mới. Không cần cài lại APK.

Đã rà soát `db`, migration EF và DataSeeder: migration tạo schema/seed system_configs; chưa có bộ seed lô/chuyến Driver tương đương. Script mới đã được kiểm tra tĩnh, chưa thực thi trên database của bạn.
