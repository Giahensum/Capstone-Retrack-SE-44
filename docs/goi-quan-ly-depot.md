# Gói quản lý Depot và tích hợp các role — 25/11/2026

## Quy tắc chung

- `BUSINESS_MODEL=DEPOT_SUBSCRIPTION`: chỉ bán phần mềm quản lý cho Depot; Seller, Employee, Driver, Factory miễn phí.
- Một gói đầy đủ, mặc định **199.000đ/kho/tháng lịch**; Admin đổi giá cho đơn mới. Một Owner nhiều kho có hạn riêng từng kho. Chưa tự gia hạn/trừ tiền.
- Kho đã xác minh có thể bắt đầu dùng thử 14 ngày **một lần**. Bootstrap/seed không tự bắt đầu dùng thử; khi demo cần Owner vào đúng kho → Gói dịch vụ → Dùng thử.
- Hết hạn vẫn đọc dữ liệu, đối soát và hoàn tất đơn đã nhận/lô đã mua. Nghiệp vụ mới cần còn hạn; API trả 402 với mã `SUBSCRIPTION_REQUIRED`. Mua gói không thay việc duyệt hồ sơ.
- Giao dịch mới không có hoa hồng/thu hộ vào sàn. Tiền hàng do hai bên thanh toán theo thỏa thuận và ghi nhận trên RETRACK. Không xóa/đổi phí đã chốt hoặc các khoản tiền/hoàn/chi cũ.

## Tiền gói

Owner lập đơn một tháng có `operationId`; backend giữ snapshot số tiền và chỉ một đơn chờ/kho. `PENDING` chưa cấp quyền; upload chứng từ cũng chưa cấp quyền. Admin kiểm tra tiền thực nhận rồi duyệt, hoặc yêu cầu bổ sung có lý do. PayOS link/webhook/sync dùng đúng đơn, chữ ký/số tiền; redirect trình duyệt không tự kích hoạt.

`PAID` cộng một tháng lịch vào mốc lớn nhất: hiện tại, hạn dùng thử, hạn đã trả. Khóa dòng Depot và transaction giữ kích hoạt một lần khi retry/cạnh tranh. Worker thử đối soát lại các đơn PayOS chờ, không giữ transaction khi gọi mạng.

PayOS dùng cấu hình `PayOS__*` local hiện có và webhook `/api/payos/webhook`. Không thêm key vào Git, không tự bật tài khoản/giả lập thành tiền thật. Phiếu mua gói là ghi nhận dịch vụ, chưa thay hóa đơn điện tử hợp pháp; chủ thể vận hành/thuế còn cần nhóm chốt.

## Hoàn tiền gói

Tách hẳn với tiền hàng: RETRACK không dùng tiền gói để hoàn tiền hàng. Bảng `depot_subscription_refunds` (khối SQL `RETRACK_SUBSCRIPTION_REFUND_V1`), mỗi bước ghi `audit_logs` và báo Owner (category `SUBSCRIPTION`).

| Loại (`kind`) | Đơn hợp lệ | Khi Admin duyệt |
|---|---|---|
| `DUPLICATE_OR_EXCESS` | `PAID` | Hoàn phần trùng/thừa, cần chứng từ/mã giao dịch; không đổi hạn dùng |
| `SHORT_PAYMENT` | `PENDING` có chứng từ (chưa PayOS) hoặc `CANCELLED` | Hoàn khoản đã nhận; đơn chờ bị đóng để lập đơn mới đúng giá |
| `UNUSED_PERIOD` | `PAID`, kỳ chưa bắt đầu **và** là kỳ cuối (`PaidUntil = PeriodEnd`) | Hoàn đủ giá đơn, đặt `period_revoked_at`, `PaidUntil` lùi về hết kỳ trước; các kỳ khác giữ nguyên |
| `SERVICE_ERROR` | `PAID` | Admin xác minh, hoàn toàn bộ/một phần; tổng hoàn của đơn ≤ số tiền đơn |
| `LATE_PAYMENT` | `CANCELLED` | Tiền về sau khi đơn đóng được đối soát, không tự kích hoạt gói |

Kỳ đang sử dụng không hoàn vì ngừng dùng. Trạng thái: `REQUESTED` → `APPROVED` (đã duyệt, **chưa** chuyển tiền) → `REFUNDED` (Admin ghi mã giao dịch chuyển trả), hoặc `REJECTED` có lý do. Mỗi đơn chỉ một yêu cầu đang chờ; `operationId` chống gửi lặp. Owner gửi ở `/depot/subscription`, Admin xử lý ở `/admin/billing` (cũng có thể tạo hộ yêu cầu). API: `GET/POST /api/depot/subscription/refunds`, `GET/POST /api/admin/subscriptions/refunds`, `POST .../refunds/{id}/decision`, `POST .../refunds/{id}/transferred`.

Dùng thử chống lách: một mã số thuế chỉ được dùng thử một lần dù đăng ký nhiều kho. Dashboard Admin hiển thị doanh thu gói, tiền đã hoàn, doanh thu ròng, số kho đang dùng/dùng thử/hết hạn. Trong chế độ gói, cấu hình phí nền tảng và hoa hồng bị khóa (chỉ để tra lịch sử).

## Trang công khai

`/goi-dich-vu` (giới thiệu gói, giá/ngày dùng thử lấy từ `GET /api/public/depot-plan`, không cần đăng nhập) và `/dieu-khoan` (điều khoản, hoàn tiền, quyền riêng tư). Không nêu tên doanh nghiệp, mã số thuế, thuế suất; ghi rõ chứng từ nội bộ không phải hóa đơn điện tử hợp pháp.

## Contract các role cần giữ

| Luồng | Quy tắc mới / tương thích |
|---|---|
| Owner → Seller | Body báo trả tiền thêm `paymentMethod`: `BANK_TRANSFER` (mặc định cũ), `CASH`, `OTHER`; `paymentReference` là căn cứ tiền mặt/khác. Chuyển khoản vẫn cần `paymentProofUrl`. Seller xác nhận thực nhận mới `DONE` và tăng tồn |
| Factory → Owner | Báo thanh toán có cùng ba hình thức; bank cần attachment hợp lệ, cash/other cần reference. Giá hai bên đồng ý trước; Owner xác nhận thực nhận mới `PAID` |
| Lô lớn | Trả giá cả lô, tiền/cọc thỏa thuận ngoài app; tham chiếu có thể là mã chuyển khoản hoặc biên nhận tiền mặt. Nhận đủ rồi Owner nhập một lần theo kg/cái/bộ |
| Tiền qua sàn cũ | Endpoint tiền trả mode theo nguồn. Khoản cũ có collection PENDING/PAID/REFUNDING vẫn dùng platform để đối soát/chi/hoàn; không chỉ nhìn `money.mode` toàn cục |
| Employee/dispatch | Đơn mới chỉ nhận/phân công vào kho còn quyền dùng. Đơn đã nhắm/gán kho vẫn được hoàn tất. Mobile phải dùng mode từ server thay vì tự coi mọi đơn đều trả PayOS |
| Admin | Doanh thu nền tảng mới là số tiền đơn gói PAID, không cộng tiền hàng. Menu phí/thu chi cũ dùng tra lịch sử |

Không đổi các mã status pickup: `SELLER_CONFIRMED` vẫn chờ Employee bàn giao, `AWAITING_PAYMENT` mới Owner báo trả, `PAYMENT_SENT` chờ Seller nhận, `DONE` kết thúc. Trả tiền mặt không tự skip xác nhận hai phía.

## Cài đặt DB

Chỉ dùng `db/bootstrap/retrack-system.sql`. Máy mới chạy toàn file trong pgAdmin. DB cũ sao lưu và áp schema trước `-- RETRACK_SCHEMA_END` kèm `COMMIT;`, không seed. Có thêm hai bảng gói, sequence/index và cột hình thức thanh toán; giữ bảng Money/lịch sử. Không `dotnet ef database update`, không tự migrate khi chạy app. Chi tiết ở [db/README.md](../db/README.md).

## Phần quản lý gọn hơn

Nhập lô lớn có hai nút thêm phế liệu/đồ cũ, nguồn/giá trúng tự lấy; chi phí phân bổ mở khi cần. Tab Đang mua dẫn ngay tới hành động tiếp theo, lô ghi sổ sang Lịch sử. Trước xuất có xem giá vốn FIFO/lãi dự kiến; thiếu giá vốn ghi chưa xác định. Đây là hỗ trợ quản lý, chưa là kế toán thuế đầy đủ; tiền gói tháng chưa tự phân bổ vào từng lô.

Nghiệm thu PayOS thật/public webhook, mobile/GPS, tải/deploy và hóa đơn pháp lý phải làm riêng trước vận hành thật. Không coi kiểm thử bằng provider giả lập là giao dịch ngân hàng đã chạy thật.
