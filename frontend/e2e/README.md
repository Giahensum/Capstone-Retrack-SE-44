# Kiểm thử trình duyệt Depot – Factory

Các bài này thao tác trình duyệt thật, gọi API thật và tải lại trang để kiểm tra dữ liệu đã lưu. Không thay API bằng mock. Chỉ chạy với tài khoản và database thử nghiệm riêng.

## Chuẩn bị

1. Chạy backend Development trên cổng 5000 theo README gốc. Chuẩn bị schema và tài khoản test; không dùng database production hoặc database dùng chung.
2. Trong `frontend`, chạy `npm ci`, `npx playwright install chromium`, rồi `npm run dev`.
3. Thiết lập biến môi trường trong terminal chạy test. Không ghi mật khẩu vào file được Git theo dõi. Có thể nhập mật khẩu bằng `Read-Host -MaskInput` trong PowerShell 7.

```powershell
$env:E2E_BASE_URL = 'http://localhost:5173'
$env:E2E_DEPOT_EMAIL = Read-Host 'Email chủ kho test'
$env:E2E_DEPOT_PASSWORD = Read-Host 'Mật khẩu chủ kho test' -MaskInput
$env:E2E_FACTORY_EMAIL = Read-Host 'Email nhà máy test'
$env:E2E_FACTORY_PASSWORD = Read-Host 'Mật khẩu nhà máy test' -MaskInput
$env:E2E_DEPOT_ID = Read-Host 'UUID kho có tồn khả dụng'
npm run test:e2e -- --reporter=line --workers=1
```

Thiếu tài khoản hoặc chưa bật cờ kiểm thử có ghi dữ liệu thì các bài tương ứng **Skipped**, không được báo cáo là Passed.

Nếu backend đang mở là bản build cũ, chạy bản mới trên cổng riêng và trỏ Vite tới nó:

```powershell
# Terminal backend: build trước; giữ cấu hình local của mình, tắt seed nếu đã có dữ liệu.
$env:Database__Initialize = 'false'
dotnet run --project backend/Retrack.API --no-launch-profile -- --urls http://localhost:5010 --environment Development
# Terminal frontend:
$env:API_PROXY_TARGET = 'http://localhost:5010'
npm run dev -- --port 5174 --strictPort
# Terminal test:
$env:E2E_BASE_URL = 'http://localhost:5174'
```

`API_PROXY_TARGET` chỉ cấu hình proxy của Vite, mặc định vẫn là cổng 5000. Phải khởi động lại Vite khi đổi biến này. Kiểm tra Swagger của backend có `/api/depot/reports/invoices/{id}/simulate-payment` trước khi thử PayOS.

## Ảnh vật liệu

Backend cần có cấu hình Cloudinary hợp lệ trong `.env` local; trình duyệt cần truy cập được CDN. Kho cần ít nhất 1 kg tồn khả dụng. Nhà máy test phải xem được vật liệu của kho và không bị chặn hợp tác.

```powershell
$env:E2E_UPLOAD = 'true'
npm run test:e2e -- e2e/depot-batches.spec.js --reporter=line
```

Bài test chọn ảnh PNG tổng hợp, tạo lô, kiểm tra ảnh sau reload ở Depot và mở ảnh ở Factory. Khối `finally` hủy lô chưa được nhận để giải phóng tồn. Bản ghi lô đã hủy và ảnh tổng hợp vẫn được giữ làm bằng chứng; không tự xóa lịch sử hoặc tài sản Cloudinary của người dùng.

## Cân, KCS và quyết toán

Kho cần ít nhất 1 kg PET khả dụng. Bộ kiểm thử tự tạo lô có tiền tố `E2E-Depot-Factory-`, Factory nhận qua giao diện. **Chỉ đoạn vận chuyển được thay bằng fixture DELIVERED**, chưa phải E2E Driver/TV3. Các bước nhận tại nhà máy, cân, KCS, đính kèm phiếu cân/hóa đơn, quyết toán và đọc lại tại Depot đều thao tác qua giao diện.

```powershell
$env:E2E_TRANSPORT_FIXTURE = 'true'
$env:E2E_PSQL_PATH = 'C:/Program Files/PostgreSQL/17/bin/psql.exe'
$env:PGHOST = 'localhost'
$env:PGPORT = '5432'
$env:PGDATABASE = 'Retrack_TV2_dev'
$env:PGUSER = Read-Host 'Người dùng PostgreSQL local'
$env:PGPASSWORD = Read-Host 'Mật khẩu PostgreSQL local' -MaskInput
npm run test:e2e -- e2e/depot-factory-settlement.spec.js --reporter=line
```

`PGDATABASE` phải trùng database backend đang chạy. Fixture từ chối host ngoài local và database ngoài `Retrack_TV2_dev`/`Retrack_TV2_test`; chỉ cập nhật đúng chuyến PENDING của lô có tiền tố thử nghiệm và khóa dòng depot trước khi cập nhật. Không reset database, không dọn dữ liệu của role khác.

Mỗi lần chạy thành công giữ lại một lô đã quyết toán 1 kg PET với giá thử 12.000 đ/kg. Đây là ghi nhận mô phỏng, không chuyển tiền ngân hàng. Khi bài thất bại, lô dừng ở bước đã thực hiện; kiểm tra các lô mang tiền tố thử nghiệm trước lần báo cáo tiếp theo.

## Kiểm chứng và giới hạn

### PayOS giả lập

Chuẩn bị một hóa đơn phí riêng có trạng thái `UNPAID` ở trang đầu danh sách; đặt `E2E_PAYOS_INVOICE_ID` bằng UUID hóa đơn. Với frontend/backend Development, chạy `npm run test:e2e -- e2e/depot-payos.spec.js --reporter=line`. Bài này thay hóa đơn thành `SUBMITTED` (chờ Admin đối soát), kiểm tra sau reload và không gọi PayOS/chuyển tiền thật. Không dùng hóa đơn thật và không tự đổi lại trạng thái hóa đơn. PayOS production cần tích hợp riêng sau khi có cấu hình nhà cung cấp.

- `node --test src/features/depot/proofDownload.test.js`: kiểm tra định dạng/chữ ký chứng từ, chặn HTML/SVG, dữ liệu lỗi và quá cỡ.
- `npm run lint`, `npm run build`: kiểm tra source frontend.
- `test-results` và `playwright-report` là artifact local đã ignore. Trace có thể chứa phiên đăng nhập; không commit hoặc chia sẻ nguyên trace công khai.
- Bộ test này không thay thế kiểm thử API sai quyền, race/rollback trên PostgreSQL, PayOS thật, Driver, hay kiểm thử toàn bộ use case các role khác.
- Bộ backend cần cả `RETRACK_TEST_CONNECTION` và `RETRACK_TEST_POSTGRES` trỏ tới `Retrack_TV2_test`, rồi chạy `dotnet test backend/Retrack.sln --no-restore` từ gốc repo.
