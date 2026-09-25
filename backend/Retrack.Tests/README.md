# Kiểm thử hồi quy PostgreSQL cho Chủ kho vựa

**Người phụ trách: Ngô Sỹ Giá (TV2 — Depot Owner).**

Cần PostgreSQL local và database riêng có tên chính xác `Retrack_TV2_test`.
Đặt `RETRACK_TEST_CONNECTION` trong biến môi trường bằng thông tin kết nối local;
không commit thông tin đăng nhập. Bộ kiểm thử từ chối database tên khác và tự áp dụng
migration EF. Các ca gửi đồng thời dùng DbContext độc lập; sau mỗi ca chỉ dọn dữ liệu
mẫu do chính ca đó tạo. Không dùng database chứa dữ liệu chung.

```powershell
dotnet test backend/Retrack.Tests/Retrack.Tests.csproj
```

Phạm vi gồm quyền chủ kho đang hoạt động, trạng thái cho phép, URL chứng từ,
gửi lại cùng dữ liệu, gửi lại dữ liệu khác, chỉ ghi một phí khi nhiều yêu cầu đồng thời,
chứng từ cạnh tranh, đơn không tồn tại và hoàn tác khi ghi phí thất bại.

Bộ kiểm thử còn bao gồm giữ/hủy tồn kho đồng thời, tạo lô an toàn khi gửi lại,
quan hệ nhà máy, mã lô tự sinh, validation hồ sơ, tạo/khóa nhân viên, truy vấn báo cáo,
gửi chứng từ hóa đơn và chặn vai trò đặc quyền khi đăng ký công khai. Khởi tạo migration
được tuần tự hóa giữa các lớp, không tắt kiểm thử giao dịch đồng thời.
**Kết quả lần chạy gần nhất: 46 đạt, 0 lỗi, 0 bỏ qua.**

Bằng chứng trình duyệt/HTTP thật và phần còn thiếu: `docs/depot-integration.md`
(tính đường dẫn từ thư mục gốc repository).

Lần chạy hồi quy đầu với hàm thanh toán cũ có 19 ca lỗi và một ca thanh toán thông thường
đạt trên PostgreSQL. Kết quả chứng minh các lỗi thiếu kiểm tra quyền/trạng thái/URL,
ghi phí trùng và lưu dữ liệu không đầy đủ khi ghi phí thất bại.
