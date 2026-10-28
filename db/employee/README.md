# SQL cho Depot Employee

Máy mới chỉ cần chạy `db/bootstrap/retrack-system.sql`: schema và seed demo chung đã có trong file này. Không cần chạy thêm các file dưới đây nếu chỉ muốn dùng bộ demo chung.

Các file được giữ lại:

| File | Mục đích |
|---|---|
| `02_employee_demo.sql` | Bộ 13 đơn Đà Nẵng riêng, UUID `e0000000-0929-*`, chưa có trong bootstrap. Chỉ chạy khi cần fixture này. |
| `04_employee_dispatch.sql` | Bản nâng cấp phân công và check-in cho DB cũ; giữ riêng theo yêu cầu. Schema tương ứng đã có trong bootstrap. |
| `05_dispatch_danang_demo.sql` | Bộ 36 đơn, 6 nhân viên, 6 khu vực kiểm thử phân công; giữ riêng theo yêu cầu. |

Đã xóa `01_employee_setup.sql` và `03_notification_pickup_link.sql` vì chức năng đã có trong bootstrap. Phần ghép thông báo Employee cũ với đơn đã được bổ sung vào bootstrap. Cấu hình phí đang có được giữ nguyên, không áp lại giá trị 5% như script setup cũ.

Với DB cũ: chỉ chạy khối schema đã rà soát, trong transaction riêng, kết thúc trước `-- RETRACK_SCHEMA_END`; không chạy kèm seed mặc định. Riêng nâng cấp phân công có thể dùng file 04. Không xóa bảng để cập nhật schema.

File 02 yêu cầu tài khoản `employee@retrack.vn`, `seller@retrack.vn` và liên kết nhân viên–kho đang hoạt động. Mặc định chỉ bổ sung đơn chưa tồn tại. Tùy chọn `retrack.reset_employee_demo = 'true'` chỉ dành cho DB test cá nhân: từ chối reset đơn đã có thanh toán/tồn kho. Sau khi dùng phải đổi lại `'false'`.

Hướng dẫn luồng phân công mới: `docs/EMPLOYEE_AUTO_DISPATCH_TEST.md`.
