# SQL Driver

Máy mới chỉ cần chạy `db/bootstrap/retrack-system.sql`: đã gồm schema Driver, nhật ký giao nhận, liên kết thông báo và seed demo chung.

Đã xóa `01_driver_setup.sql` và `03_driver_delivery.sql` vì schema đã được hợp nhất. Với DB cũ, rà soát và chạy khối schema trong transaction riêng, kết thúc trước `-- RETRACK_SCHEMA_END`; không chạy kèm seed mặc định và không xóa bảng.

Hai fixture sau **chưa trùng dữ liệu bootstrap**, nên được giữ lại, chỉ chạy khi cần mở rộng kiểm thử:

| File | Dữ liệu bổ sung |
|---|---|
| `02_driver_demo.sql` | 3 lô `DRV-DEMO-01/02/03`, UUID lô `d6100000-*`, chuyến `d6200000-*`; dùng kho của Driver và nhà máy phù hợp hiện có. |
| `04_driver_danang_local_demo.sql` | 3 lô `DRV-DN-LOCAL-01/02/03`, UUID lô `d8500000-*`, chuyến `d8600000-*`; thêm kho/nhà máy giả lập cùng tọa độ X7P4+XF9 Đà Nẵng. |

Cần tài khoản `driver@retrack.vn`, liên kết kho đang hoạt động và tài khoản Factory phù hợp. File 04 tạo tài khoản `factory.driver-local@retrack.test`, sao chép hash từ tài khoản Factory nguồn khi tạo lần đầu; không thay mật khẩu tài khoản cũ.

File 02 thêm lô, chuyến và thông báo. File 04 còn thêm tài khoản Factory, kho, nhà máy và liên kết nhân viên của fixture. Không tạo đơn Seller/Employee. Các fixture này không dùng để kiểm chứng cân đối tồn kho đầu vào Depot.

Chạy lại giữ nguyên chuyến đã test; dữ liệu fixture thiếu một phần sẽ báo lỗi, không tự reset. Không cần xóa bảng. Kho và nhà máy file 04 cùng tọa độ nên không phù hợp để kiểm thử tuyến Goong.

Với code check-in hiện tại: chụp ảnh lấy hàng → xác nhận → khởi hành → chụp ảnh giao hàng → xác nhận. Không bắt buộc GPS hoặc bán kính 200 m; vẫn kiểm tra quyền tài xế, trạng thái chuyến và ảnh hợp lệ.
