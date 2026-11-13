# Thông báo Seller

Nhánh: `feature/TV1-seller-notifications`. Phạm vi: backend dùng chung cho Seller và giao diện web Seller.

## Mốc nghiệp vụ

| Luồng | Mốc | Người nhận |
|---|---|---|
| Thanh lý | Tạo lô `PUBLISHED`, hủy lô `CANCELLED` | Seller; khi hủy vẫn giữ thông báo cho kho tham gia |
| Thanh lý | Kho đăng ký và lần đầu xác nhận khảo sát | Seller |
| Thanh lý | Lượt trả giá hợp lệ mới | Seller; giữ thông báo bị vượt giá cho kho trước đó |
| Thanh lý | Chốt `AWARDED` hoặc `NO_SALE` | Seller; giữ thông báo kết quả cho kho theo luồng hiện có |
| Thỏa thuận | Kho ký hợp đồng, báo chuyển cọc | Seller, giữ hành vi đã có |
| Thỏa thuận | Kho báo chuyển phần tiền thanh toán | Seller, yêu cầu đối chiếu tiền thực nhận |
| Thỏa thuận | Kho xác nhận thu gom | Seller; việc hoàn tất vẫn cần cả hai mốc giao nhận |
| Thỏa thuận | Seller ký, xác nhận cọc, xác nhận đủ tiền, bàn giao | Giữ thông báo cho kho theo luồng hiện có |
| Thu gom | Tạo đơn, nhận/phân công đơn | Seller |
| Thu gom | Check-in, kết quả cân, chờ chủ kho thanh toán | Seller; bổ sung đủ nhóm và liên kết đơn cho thông báo mới |
| Thu gom | Seller đồng ý kết quả cân | Nhân viên được giao đơn |
| Thu gom | Seller yêu cầu cân lại | Seller và nhân viên được giao đơn |
| Thu gom | Seller hủy đơn `PENDING` | Seller |
| Thu gom | Kho báo chuyển tiền `PAYMENT_SENT` | Seller |
| Thu gom | Seller xác nhận nhận tiền, đơn `DONE` | Seller và chủ kho |

Lượt trả giá mới kèm thời gian đóng phiên hiện tại sau khi áp dụng quy tắc gia hạn. Chưa bổ sung lịch nhắc trước giờ mở/đóng phiên, quá hạn thanh toán hoặc quá hạn thu gom vì chưa có chính sách thời điểm/tần suất nhắc. Tạo lô không phát thông báo đại trà cho tất cả kho.

## Tính nhất quán

- Thông báo được ghi cùng transaction với thay đổi nghiệp vụ. Lỗi ghi thông báo thanh toán phải rollback cả trạng thái, chứng từ và phí.
- Giữ khóa dòng khi tạo/chốt lô và thao tác thỏa thuận; thêm khóa đơn cho quyết định cân và hủy đơn Seller để tránh cập nhật đồng thời ghi đè nhau.
- Retry có cùng mã thao tác tạo lô/trả giá, báo chuyển tiền hoặc xác nhận thu gom không sinh thông báo trùng. Quyết định cân/hủy đơn lặp lại bị chặn khi trạng thái đã đổi; không phát thêm thông báo.
- `LIQUIDATION` dùng `TargetType=LIQUIDATION`, `TargetId=lotId`. Thu gom dùng `PickupRequestId`, `TargetType=PICKUP_REQUEST`, `TargetId=requestId`.
- Giao diện chỉ tạo đường dẫn nội bộ từ loại đích đã hỗ trợ và UUID hợp lệ. Giữ bộ lọc, phân trang và quyền sở hữu của API thông báo hiện có.
- Chuông, danh sách và số chưa đọc dùng React Query, đồng bộ sau mutation; khi đang mở trang, tải lại theo chu kỳ 30 giây. Đây là thông báo trong ứng dụng, chưa có push/SignalR/email.
- Thay đổi metadata áp dụng cho thông báo mới, không sửa dữ liệu thông báo cũ.

## Phụ thuộc ngoài Seller

- `DepotPaymentService` và repository thanh toán: chuyển việc ghi thông báo vào transaction thanh toán thay vì ghi sau commit trong `PickupService`.
- Luồng Employee: giữ thông tin đơn và nhóm `PICKUP` ở các thông báo check-in, nhận đơn, gửi cân và bàn giao.
- `INotificationService.SendPickupAsync` bổ sung tương thích cho luồng nhận đơn Employee; không thay client hay kho token.
- `TestDatabase`: chuẩn bị riêng `Retrack_TV2_test` bằng toàn bộ phần schema trước `RETRACK_SCHEMA_END`, tắt seed demo, commit transaction SQL. Bộ khởi tạo cũ trộn migration và một phần schema dẫn đến thiếu `employee_collection_events`. Không sửa migration hay SQL bootstrap và không thay cơ chế khởi động ứng dụng.

## Kết quả kiểm chứng

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore backend/Retrack.sln` | Passed |
| `dotnet build backend/Retrack.sln --no-restore` | Passed |
| `dotnet test backend/Retrack.sln --no-build --no-restore` | Passed: 200 + 295 = 495, không skip, sau khi bổ sung bộ kiểm thử từng API |
| `npm.cmd ci` trong `frontend` | Passed |
| `npm.cmd run lint` | Passed, còn cảnh báo scaffold rỗng/import có sẵn |
| `npm.cmd run build` | Passed, còn cảnh báo bundle lớn có sẵn |
| Playwright `seller-notifications.spec.js` | Passed: 1, Chromium, API thật và PostgreSQL thật |

Các ca backend kiểm tra tạo lô retry/race, thông báo đăng ký/khảo sát/trả giá/chốt phiên, `NO_SALE` không có người tham gia, báo chuyển tiền và thu gom lặp lại, sai Seller, xác nhận/từ chối cân, thanh toán đồng thời, rollback khi lưu phí hoặc thông báo lỗi và truy vấn lại từ DbContext mới.

Ca trình duyệt tạo lô qua API rồi gửi lại cùng thao tác, đăng nhập bằng biểu mẫu, lọc đấu giá, mở chi tiết lô từ danh sách và chuông, đánh dấu chưa đọc/đọc tất cả, tải lại để kiểm tra persistence, đồng thời kiểm tra Seller khác không đánh dấu thông báo của chủ lô. Ca này không thao tác toàn bộ biểu mẫu tạo lô hoặc toàn bộ vòng đời đấu giá trên trình duyệt; các bước chốt phiên và thỏa thuận được kiểm chứng ở backend.

## Chạy lại

1. Đặt cả `RETRACK_TEST_CONNECTION` và `RETRACK_TEST_POSTGRES` tới cùng database riêng `Retrack_TV2_test`, rồi chạy lệnh backend ở trên. Bộ test không reset/drop database.
2. Với trình duyệt, chạy API riêng bằng biến môi trường `ConnectionStrings__DefaultConnection` tới database test, `Database__Initialize=false`; chạy Vite với `API_PROXY_TARGET` trỏ tới API này.
3. Đặt `E2E_BASE_URL` tới Vite, `E2E_TARGET_DATABASE=Retrack_TV2_test`, `E2E_SELLER_EMAIL` là email fixture mới và `E2E_SELLER_PASSWORD` là mật khẩu tạm qua biến môi trường. Không dùng tài khoản thật.
4. Chạy `npx.cmd playwright test e2e/seller-notifications.spec.js --reporter=line` trong `frontend`. Thiếu biến môi trường thì ca trình duyệt sẽ skip. Tắt trace cho ca này để không ghi thông tin đăng nhập vào artifact.
5. Sau kiểm thử trình duyệt, chỉ dọn hai user có email fixture và email thêm `-other` trước `@`, cùng notification/lô/đơn của đúng hai user đó, trong database test. Không dọn theo tiền tố chung. Fixture của lần kiểm chứng nêu trên đã được dọn.

Giới hạn: chưa kiểm thử mobile, push hoặc email; không cần migration/schema mới và không có thao tác trên database ứng dụng dùng chung.

## Ma trận kiểm thử từng API thông báo Seller

Đã bổ sung 58 ca controller/service trong [SellerNotificationControllerTests.cs](../backend/Retrack.API.Tests/SellerNotificationControllerTests.cs) và 12 ca HTTP trong [SellerNotificationHttpTests.cs](../backend/Retrack.API.Tests/SellerNotificationHttpTests.cs). Cả hai bộ đều sử dụng SQLite riêng trong bộ nhớ, không cần kết nối PostgreSQL và không ghi vào database ứng dụng.

| API | Các trường hợp đã kiểm tra |
|---|---|
| `GET /api/seller/notifications` | Chỉ lấy thông báo của Seller đang đăng nhập; thứ tự thời gian giảm dần và UUID tăng dần khi trùng thời gian; DTO không chứa entity graph; message null thành chuỗi rỗng; giữ TargetType/TargetId; danh sách rỗng; từng nhóm PICKUP/PAYMENT/LIQUIDATION/SYSTEM; nhóm không tồn tại/khác chữ hoa; nhóm trống/khoảng trắng; kết hợp unreadOnly=true/false; tổng sau lọc; trang đầu/giữa/cuối/vượt phạm vi; page <= 0; pageSize <= 0, 100, 101 và int.MaxValue; page=int.MaxValue; phân trang không lặp/mất bản ghi cùng thời gian |
| `GET /api/seller/notifications/unread-count` | Đếm tất cả nhóm chưa đọc của đúng Seller; bỏ thông báo đã đọc và của Seller khác; tài khoản chưa có thông báo trả 0; số đếm thay đổi khi đọc/chưa đọc |
| `PATCH /api/seller/notifications/{id}/read` | Chưa đọc sang đã đọc; đọc lại trả thành công; lưu được qua DbContext mới; giữ nguyên nội dung, đích và thời gian tạo; không thêm notification; ID không tồn tại, Guid.Empty và ID Seller khác đều trả cùng 404; không làm đổi dữ liệu Seller khác |
| `PATCH /api/seller/notifications/{id}/unread` | Đã đọc sang chưa đọc; đánh dấu chưa đọc lại trả thành công; lưu qua DbContext mới; giữ metadata; ID không tồn tại, Guid.Empty và ID Seller khác trả 404; không thay đổi dữ liệu khác |
| `PATCH /api/seller/notifications/read-all` | Chỉ cập nhật thông báo chưa đọc của Seller hiện tại; trả đúng số bản ghi thay đổi; chạy lại trả 0; toàn bộ đã đọc/tài khoản rỗng trả 0; không đánh dấu thông báo của Seller khác |
| Cả 5 API | Thiếu/sai claim định danh không đọc hoặc ghi dữ liệu; CancellationToken đã hủy được truyền xuống truy vấn và không thay đổi dữ liệu; HTTP không đăng nhập trả 401; DEPOT_OWNER/DEPOT_EMPLOYEE/DRIVER/FACTORY/ADMIN trả 403; SELLER hợp lệ được truy cập |
| Ràng buộc HTTP | UUID không hợp lệ không khớp route PATCH và trả 404; page/pageSize không phải số hoặc vượt Int32 trả 400; unreadOnly không phải boolean trả 400 |
| Dịch vụ gửi thông báo thu gom | `SendPickupAsync` lưu đúng người nhận, nội dung, nhóm PICKUP, PickupRequestId, TargetType/TargetId; thông báo đọc lại được qua API lọc PICKUP/unread |

Bộ HTTP chạy Kestrel và model binding/authorization thật, nhưng thay xác thực JWT bằng handler fixture chỉ nằm trong project test. Kết quả 401/403 xác minh policy và route của controller, không xác minh chữ ký JWT, token hết hạn hoặc khóa tài khoản trong `Program.cs`. Các ca gọi controller trực tiếp không đi qua middleware HTTP.

## Kiểm tra sự kiện nghiệp vụ và rollback bổ sung

- [PaymentTests.cs](../backend/Retrack.Tests/PaymentTests.cs): thêm 11 ca hủy đơn (9 trạng thái và 2 ca sai người dùng/không tồn tại). Chỉ PENDING phát thông báo, retry không sinh trùng; các trạng thái bị từ chối không tạo notification. Giữ các ca thanh toán retry/race/rollback và quyết định cân đã có.
- [SellerLiquidationTests.cs](../backend/Retrack.Tests/SellerLiquidationTests.cs): thêm ca lỗi ghi notification phải rollback cả lô vừa tạo; bổ sung kiểm tra thông báo hủy lô gửi Seller chỉ một lần. Giữ các ca tạo lô, đăng ký/khảo sát, trả giá, chốt AWARDED/NO_SALE, chuyển tiền, thu gom, sai quyền và retry đã có.
- [EmployeePickupTests.cs](../backend/Retrack.API.Tests/EmployeePickupTests.cs) và [EmployeeCollectionTests.cs](../backend/Retrack.API.Tests/EmployeeCollectionTests.cs): bổ sung assertion nhóm PICKUP và liên kết đơn cho thông báo nhận đơn/check-in, gồm retry không tạo trùng.
- Các ca PostgreSQL chỉ dùng `Retrack_TV2_test`, dọn đúng fixture của từng ca, không reset/drop database.

Test biên phát hiện lỗi tràn số ở `(page - 1) * pageSize`: page=int.MaxValue và pageSize=100 từng trả lại trang đầu. Controller đã đổi phép tính sang long và giới hạn offset theo tổng bản ghi; ca chống tái phát đã qua.

Kết quả chạy bổ sung: build Passed; `Retrack.Tests` 200/200 và `Retrack.API.Tests` 295/295, tổng 495/495, không Failed/Skipped. Lượt này không sửa frontend và không chạy lại lint/build/Playwright; các kết quả frontend ở bảng trên là của lần triển khai trước.

Chạy riêng bộ controller/service và HTTP mới, không cần database ngoài:

```powershell
dotnet test backend/Retrack.API.Tests/Retrack.API.Tests.csproj --no-restore --filter "FullyQualifiedName~SellerNotificationControllerTests|FullyQualifiedName~SellerNotificationHttpTests"
```

Chạy hồi quy toàn bộ sau khi đặt hai biến kết nối test đúng database:

```powershell
dotnet build backend/Retrack.sln --no-restore
dotnet test backend/Retrack.sln --no-build --no-restore
```
