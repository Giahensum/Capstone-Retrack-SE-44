# Tích hợp Chủ kho vựa — báo cáo triển khai và kiểm chứng

**Người phụ trách: Ngô Sỹ Giá (TV2 — Depot Owner).**

Mốc bắt đầu: `origin/develop` tại `cd35f33`. Nhánh hiện tại: `feature/ngo-sy-gia-depot-backend-integration` (đã đổi từ nhánh TV2 theo yêu cầu Ngô Sỹ Giá).
Trước khi triển khai, thư mục làm việc không có thay đổi. Đã lưu ba commit local theo nhóm backend, frontend và tài liệu để bảo toàn công việc trước khi tích hợp develop. Chưa push hay triển khai lên máy chủ. Tên database có chữ TV2 được giữ nguyên để bảo toàn cấu hình đang sử dụng.

## Kiến trúc đã xác minh

- API chạy ở `backend/Retrack.API`, dùng .NET 8, EF Core 8 và Npgsql.
- Kiểu dữ liệu phản hồi đang dùng nằm trong `DTOs/AppDtos.cs`; repository đang hoạt động nằm trong `Repositories/IRepositories.cs` và `Repositories/Repositories.cs`.
- `Helpers/ApiResponse.cs`, `Repositories/Interfaces` và nhiều controller/service theo vai trò còn là khung chờ triển khai. Không tạo thêm bản sao của kiểu dữ liệu đang dùng.
- `AppDbContext` ánh xạ các trạng thái dạng chuỗi và các thực thể trong `Models/OtherModels.cs`. Một số model/enum độc lập chưa được ánh xạ vào database.
- `Depot.OwnerId` không duy nhất: một chủ kho có thể quản lý nhiều kho. Mọi truy vấn phải kiểm tra chủ sở hữu và kho đang chọn.
- Các trang Depot thực tế được router sử dụng nằm trực tiếp trong `frontend/src/features/depot`; thư mục `pages/` chứa khung giao diện cũ.
- Đăng nhập dùng Zustand, `PrivateRoute` và Axios dùng chung. `AuthProvider` hiện còn là khung.

## Quyết định nghiệp vụ và giới hạn tích hợp

- Tài liệu yêu cầu phí mặc định 5%, dữ liệu khởi tạo cũ dùng 1%. Giá trị khởi tạo mới là 5%; migration chỉ thay cấu hình còn đúng `1.00`, giữ nguyên cấu hình đã tùy chỉnh và mức phí đã chốt trên từng đơn cũ.
- Dùng luồng chi tiết `SELLER_CONFIRMED → AWAITING_PAYMENT → PAYMENT_SENT → DONE`; không thay bằng luồng rút gọn trong tài liệu tổng quan.
- Hóa đơn phí tháng dùng thực thể mới `PlatformFeeInvoice`, tách biệt với `Invoice` cho giao dịch lô hàng chưa được ánh xạ. Chủ kho gửi chứng từ: `UNPAID → SUBMITTED`; chức năng Admin đối soát sau này mới chuyển sang `PAID`.
- Hồ sơ kho bổ sung mã số thuế, số điện thoại liên hệ và mô tả. Chưa có dữ liệu cước vận chuyển trong model dùng chung, nên giao diện hiển thị dấu gạch ngang, không tự tính thu nhập tài xế.
- Mã lô theo dạng `LO-YYMM-XXX`, lấy tháng theo giờ Việt Nam và số tăng từ PostgreSQL. Phần số có tối thiểu ba chữ số, tăng trên toàn hệ thống, không đặt lại mỗi tháng và có thể bị khuyết khi giao dịch hoàn tác. Lô cũ giữ nguyên ID.
- Báo cáo dùng múi giờ UTC+7, tuần bắt đầu thứ Hai. Giới hạn kỹ thuật mỗi lần truy vấn là 367 ngày. Doanh thu lấy từ bản ghi QC được chấp nhận và có chứng từ, theo ngày tạo QC. Model chưa có thời điểm thanh toán nên đây chưa phải sổ dòng tiền theo ngày thực nhận.
- Tồn kho chỉ tăng từ đơn `DONE`; tách rõ hàng đang giữ cho lô, hàng đã xuất và tồn khả dụng. Không mặc định hàng bị nhà máy từ chối đã trở về kho.

## Bản đồ chức năng và bằng chứng

Phần Depot chạy độc lập đã được tích hợp; **chưa hoàn thành toàn bộ luồng liên vai trò**. Kiểm thử service/API không thay thế kiểm thử thao tác trên trình duyệt.

| Use Case | Giao diện và chức năng | API / dữ liệu chính | Quyền | Bằng chứng | Phần còn thiếu |
|---|---|---|---|---|---|
| 2.1–2.2, 2.21 | Dashboard, báo cáo doanh thu và chi phí thu mua | Dashboard, reports; pickup, QC | Chủ kho đang chọn | Truy vấn PostgreSQL, API 200; trình duyệt hiển thị chi phí mẫu 95.000 đ | Thời điểm quyết toán và luồng QC → thanh toán của TV4 |
| 2.3 | Xem/sửa tên, mã thuế, địa chỉ, GPS, điện thoại, mô tả | Profile; Depot | Chủ kho | Kiểm thử lưu dữ liệu, sai tọa độ, sai chủ; trình duyệt từ chối vĩ độ 100, giữ nội dung nhập, sửa và lưu được sau tải lại | Chưa thử mọi tổ hợp trường trên trình duyệt |
| 2.4–2.5 | Tồn kho và lịch sử nhập từ đơn DONE | Inventory; PickupRequestItem, InventoryBatch | Chủ kho | Kiểm thử tồn kho; Seller xác nhận làm tăng 10 kg; tải lại giao diện thấy số liệu đúng | Cần luồng xác nhận hàng bị từ chối đã trả về |
| 2.6–2.8 | Tạo lô công khai/chỉ định, danh sách, hủy và mã lô | Batches; InventoryBatch, partnership, TransportJob | Chủ kho | Kiểm thử đồng thời không vượt tồn, gửi lặp, xung đột, đối tác duyệt/chờ/chặn; trình duyệt tạo rồi hủy lô 4 kg | Luồng nhà máy nhận lô và vận chuyển thuộc TV3/TV4 |
| 2.9–2.11 | Nhu cầu nhà máy, đối tác, lọc lô đang xử lý | Partners, batches; FactoryDemand, partnership | Chủ kho đang chọn | API và trang tải dữ liệu; kiểm thử quan hệ đối tác | Các thao tác chấp nhận hợp tác/mua lô của Factory còn thiếu |
| 2.12–2.16 | Tạo nhân viên/tài xế, xem, sửa, khóa/mở tài khoản | Staff; User, DepotStaff | Chủ kho | Kiểm thử tài khoản và liên kết; API tạo/đăng nhập/khóa khiến JWT cũ nhận 401; giao diện sửa tên và tải lại vẫn giữ trạng thái khóa | Chưa tạo tài khoản qua trình duyệt; đã thử qua API |
| 2.17–2.20 | Xếp hạng, lịch sử; biểu đồ số đơn, kg, giá trị thu mua | Reports; pickup, transport | Chủ kho | Kiểm thử truy vấn và quyền; biểu đồ/lịch sử hiển thị mẫu 1 đơn, 10 kg, 100.000 đ | Thiếu nguồn dữ liệu cước tài xế |
| 2.22–2.23 | Danh sách/chi tiết thanh toán, chứng từ và phí nguyên tử | Payments; PickupRequest, PlatformTransaction | Chủ kho; Seller xác nhận nhận tiền | Trình duyệt gửi chứng từ và tải lại; một phí duy nhất; kiểm thử gửi đồng thời và hoàn tác | Employee check-in/gửi đơn và hành trình trình duyệt đủ các vai trò chưa có |
| 2.24–2.25 | Phí tích lũy, hóa đơn, gửi chứng từ chờ Admin | Reports; PlatformTransaction, PlatformFeeInvoice | Chủ kho; hóa đơn tổng hợp mọi kho của chủ | Phí mẫu 5.000 đ đối chiếu đúng; kiểm thử trạng thái/gửi lặp; giao diện gửi chứng từ hóa đơn giả và tải lại vẫn SUBMITTED | Admin phát hành hóa đơn tháng và đối soát PAID |

## API đã triển khai

Các API Depot mới yêu cầu JWT vai trò `DEPOT_OWNER`; lấy danh tính từ claims. Ngoại trừ `/owned` và đường dẫn thanh toán cũ, truyền `?depotId=<mã kho thuộc quyền quản lý>`. Truy cập kho người khác trả 403. JWT của tài khoản bị khóa hoặc đã đổi vai trò bị từ chối. Đăng ký công khai không cho tạo ADMIN/DRIVER/DEPOT_EMPLOYEE.

| Phương thức | Đường dẫn sau `/api/depot` | Chức năng |
|---|---|---|
| GET | `/owned`, `/dashboard` | Kho được quản lý; tổng quan |
| GET, PUT | `/profile` | Xem/sửa hồ sơ kho |
| GET | `/inventory`, `/inventory/receipts` | Tồn kho, lịch sử đơn DONE |
| GET, POST | `/batches` | Danh sách/tạo lô; tạo cần `operationId` để chống gửi lặp |
| PATCH | `/batches/{id}/cancel` | Hủy lô chưa được nhận |
| GET | `/partners`, `/partners/demands` | Nhà máy và nhu cầu đang hiệu lực |
| GET, POST | `/staff` | Danh sách/tạo tài khoản nhân sự |
| PUT | `/staff/{id}` | Sửa thông tin và trạng thái hoạt động |
| GET | `/payments`, `/payments/summary`, `/payments/{id}` | Danh sách/tổng hợp/chi tiết thanh toán |
| PATCH | `/pickup-requests/{id}/payment-sent` | Gửi chứng từ qua đường dẫn cũ đã bảo vệ, không cần depotId |
| GET | `/reports/revenue`, `/reports/staff`, `/reports/staff/{id}` | Báo cáo và lịch sử nhân sự |
| GET | `/reports/fees`, `/reports/fees/summary`, `/reports/invoices` | Phí và hóa đơn |
| PATCH | `/reports/invoices/{id}/confirmation` | Gửi chứng từ hóa đơn |

Xem kiểu dữ liệu cụ thể trong Swagger tại `/swagger`. Danh sách có page/pageSize (1–100), thứ tự ổn định và bộ lọc được hỗ trợ. Sai dữ liệu/trạng thái bộ lọc trả 400, sai quyền 403, không tìm thấy 404, xung đột trạng thái/gửi lặp 409. Validation tự động của ASP.NET dùng ProblemDetails; phản hồi nghiệp vụ dùng ApiResponse trong `DTOs/AppDtos.cs`.

Bổ sung `PATCH /api/SellerPickup/{id}/payment-received`: Seller xác nhận `PAYMENT_SENT → DONE`, có khóa dòng và an toàn khi gửi lại. API nhận đơn/cân cũ giới hạn nhân viên đang hoạt động và đúng kho/đơn được nhận. Chưa cho nhận đơn phát rộng khi chưa thống nhất quy tắc vị trí/bán kính; hiện nhận đơn chỉ định kho.

## Kết quả kiểm chứng ngày 24–25/09/2026

- **PostgreSQL: 46 đạt, 0 lỗi, 0 bỏ qua**, chạy `dotnet test backend/Retrack.sln --no-restore` trên `Retrack_TV2_test`. Bao gồm quyền, trạng thái, URL chứng từ, ràng buộc duy nhất, gửi đồng thời, lỗi lưu phí và hoàn tác, tồn kho, hồ sơ, nhân sự, báo cáo, hóa đơn, quan hệ nhà máy và chặn đăng ký vai trò đặc quyền. Khởi tạo migration được tuần tự hóa giữa các lớp kiểm thử; các ca giao dịch đồng thời vẫn chạy nhiều DbContext đồng thời.
- **Build backend đạt**, không có cảnh báo/lỗi biên dịch. **Build frontend đạt**; Vite còn cảnh báo có sẵn về `__dirname` trong cách nạp cấu hình tương lai.
- **Lint chạy thành công bằng Oxlint** sau khi sửa script gọi eslint chưa cài. Còn sáu cảnh báo file khung rỗng ở `hooks/useApi`, `hooks/useDebounce`, `lib/goong`, `utils/constants`, `utils/format`, `utils/validators`.
- **HTTP thật:** 15 API đọc danh sách/tổng hợp đã truy vấn thành công, Swagger 200; thiếu đăng nhập 401, Seller vào Depot 403, kho khác 403, page không hợp lệ 400. Tạo nhân viên, đăng nhập, PUT khóa rồi dùng lại JWT nhận 401. Lần thử đầu dùng sai `/partners/factories` và PATCH staff trả 404/405; đã sửa thành `/partners` và PUT trước khi ghi nhận đạt.
- **Trình duyệt:** đăng nhập thật; mở đủ 10 trang Depot. Thanh toán mẫu: 10 kg × 10.000 = 100.000 đ, phí 5.000 đ, thực trả 95.000 đ; chứng từ lưu sau tải lại, chỉ có một phí. Seller xác nhận qua API; tồn kho tăng 10 kg. Tạo lô 4 kg và hủy qua giao diện; tải lại thấy khả dụng 10 kg, giữ chỗ 0 kg. Thử API mã lô nhận `LO-2609-001`, sau đó hủy lô kiểm thử.
- **Kiểm tra bổ sung:** hồ sơ sai GPS giữ dữ liệu nhập, sửa và lưu được sau tải lại; sửa tên nhân viên mẫu vẫn giữ trạng thái khóa; hóa đơn giả gửi chứng từ và tải lại vẫn chờ đối soát; biểu đồ kg/giá trị và lịch sử nhân viên phản ánh đúng đơn mẫu.
- **Giao diện:** quan sát dashboard 1440×900 và hộp thoại hủy lô 390×844; menu điện thoại điều hướng được, bảng cuộn ngang. Đã trả kích thước trình duyệt về mặc định. Chưa đối chiếu từng điểm ảnh với toàn bộ ảnh thiết kế hoặc kiểm thử đầy đủ khả năng tiếp cận.
- **Rà soát:** `git diff --check` không báo lỗi; `.env` bị ignore và không được theo dõi. Không tìm thấy JWT key/mật khẩu DB local trong các file thay đổi bàn giao. Chưa commit/push/gộp nhánh/triển khai.

## Database và cấu hình local

Chỉ tạo/dùng hai database local riêng `Retrack_TV2_dev` và `Retrack_TV2_test`; không thay đổi các database PostgreSQL đã có khác. `.env` được nạp ở Development trước khi đọc cấu hình database/JWT; biến môi trường có sẵn được ưu tiên. Xem `.env.example` và README ở thư mục gốc; không đưa secret vào tài liệu.

Các migration bổ sung, không sửa InitialCreate gốc:

1. `20260924064439_DepotPaymentUniqueness`: duy nhất theo loại nguồn/mã nguồn của giao dịch phí.
2. `20260924131224_DepotProfileAndFeeInvoices`: trường hồ sơ, bảng hóa đơn, ràng buộc và cập nhật có điều kiện mức phí mặc định.
3. `20260924133220_DepotBatchCodes`: mã lô cho phép rỗng ở dữ liệu cũ, chỉ mục duy nhất và bộ đếm.

Đã xem SQL trong `db/depot-payment-uniqueness.sql`, `db/depot-profile-fee-invoices.sql`, `db/depot-batch-codes.sql` và áp dụng trên dev/test. Nếu database khác có phí trùng, migration đầu sẽ dừng để đối soát, không tự xóa. SQL hoàn tác không thay thế phương án phục hồi dữ liệu production.

Dữ liệu demo còn lại: đơn `22222222-2222-2222-2222-222222222201` ở DONE với một phí 5.000 đ; hai lô thử nghiệm ở CANCELLED; hai tài khoản `tv2-e2e-…@test.invalid` bị khóa (một tài khoản mang tên dữ liệu thử `TV2 E2E edited staff`). Hóa đơn giả `22222222-2222-2222-2222-222222222203` kỳ tháng 8 ở SUBMITTED với chứng từ example.com; script ở `db/depot-invoice-e2e-fixture.sql`. Mô tả hồ sơ đã trả về rỗng sau kiểm thử. Không chuyển tiền thật, gửi thông báo thật hoặc tải tài liệu lên dịch vụ bên ngoài. Tên dữ liệu thử cũ được ghi nguyên văn để đối chiếu, không phải tên người phụ trách.

## Phần chưa hoàn thành và phối hợp nhóm

**Cập nhật sau khi lấy develop `7ffae6d`:** develop đã bổ sung Factory, Driver, giá tham khảo Admin và đăng nhập Google. Các ghi nhận thiếu module bên dưới mô tả mốc kiểm thử ban đầu; không còn có nghĩa toàn bộ Factory/Driver chỉ là khung. Cần kiểm chứng tiếp luồng liên vai trò, nhất là trạng thái lô chỉ định, tồn kho khi Driver lấy hàng và doanh thu theo `SettledAt` mới. Build và kiểm thử từng module đạt chưa chứng minh các hợp đồng này đã tương thích xuyên suốt.

1. **TV1/TV3:** nối giao diện Seller xác nhận nhận tiền; triển khai Employee check-in và gửi đơn sang AWAITING_PAYMENT. Dữ liệu kiểm thử bắt đầu ở AWAITING_PAYMENT không chứng minh toàn bộ luồng trước đó.
2. **TV3/TV4:** thống nhất nguồn cước vận chuyển, quy trình trả hàng và ngày thanh toán QC; triển khai nhận lô/hợp tác, vận chuyển và quyết toán. Depot hiện đọc các trường đã ánh xạ và giữ tồn kho, không tự tạo sự kiện của vai trò khác.
3. **TV5:** phát hành hóa đơn tháng theo chủ kho và đối soát; không tự chuyển SUBMITTED thành PAID. Cần thống nhất bảng/kiểu dữ liệu hóa đơn mới với Admin trước khi đưa lên môi trường chung.
4. **Dịch vụ dùng chung:** tải chứng từ còn là khung; biểu mẫu hiện nhận URL HTTP(S). Chưa kiểm thử tải ảnh Cloudinary hay thông báo thật.
5. **Kiểm thử mở rộng:** mọi tổ hợp validation, giao diện khi phiên hết hạn, phiên trình duyệt của chủ kho thứ hai, tạo tài khoản qua giao diện và đối chiếu từng thiết kế tham chiếu.

Không đánh dấu toàn bộ UC-2.1–2.25 hoàn thành xuyên suốt khi các phần phụ thuộc trên chưa có. Quy tắc cộng tác nằm ở `AGENTS.md`; người phụ trách phần Chủ kho vựa là **Ngô Sỹ Giá**.

## Đồng bộ develop và kiểm tra kiến trúc

- Đã lấy develop tới `7ffae6d`; có 9 file xung đột: `.gitignore`, `README.md`, `AppDbContext.cs`, `AppDbContextModelSnapshot.cs`, `Program.cs`, `Retrack.sln`, `frontend/package.json`, `Partners.jsx`, `Payments.jsx`.
- Hợp nhất đăng ký dịch vụ, model, snapshot và cả hai dự án kiểm thử. Giữ truy vấn/biểu mẫu Depot, bổ sung tab duyệt yêu cầu hợp tác từ develop qua Axios dùng chung. API hợp tác nhận depotId, kiểm tra quyền và không giả định chủ chỉ có một kho.
- Sửa xung đột tên namespace/model `Factory`. Không sửa các migration đã áp dụng. Kiểm tra EF không phát hiện thay đổi model chưa có trong snapshot.
- Sau tích hợp: backend build 0 lỗi/0 cảnh báo; 46 kiểm thử PostgreSQL Depot, 19 kiểm thử Factory .NET và 6 kiểm thử trạng thái Factory frontend đạt. Các kiểm thử Factory .NET dùng InMemory, không được tính thành kiểm thử PostgreSQL.
- Giữ cấu hình PostgreSQL local trước merge. `appsettings.json` và `.env` chỉ lưu trên máy; bản mẫu cấu hình chia sẻ không có thông tin mật. Quy tắc ignore bổ sung cấu hình local, kết quả kiểm thử, coverage và tệp khóa/chứng chỉ riêng.
- **Kiến trúc chưa đủ ba lớp cho toàn bộ Depot:** đã có Controller → Service/interface. Pickup/Auth dùng repository đang có, nhưng service Depot mới vẫn dùng AppDbContext trực tiếp; chưa có repository Depot chuyên biệt. Không được báo phần repository đã hoàn thành.
- **Skill đã đọc và áp dụng:** using-agent-skills, git-workflow-and-versioning, spec-driven-development, incremental-implementation, test-driven-development, api-and-interface-design, security-and-hardening, frontend-ui-engineering, browser-testing-with-devtools trong `.agent-skills/skills`. Có bằng chứng kiểm thử lỗi trước/sau cho thanh toán, phân quyền, transaction và kiểm tra trình duyệt. Chưa tuân thủ trọn vẹn mọi tiêu chí: từng tích lũy thay đổi lớn chưa commit, lớp repository Depot còn thiếu và ma trận E2E chưa đầy đủ. Kiểm thử trình duyệt dùng công cụ CUA sẵn có thay vì Chrome DevTools MCP.
