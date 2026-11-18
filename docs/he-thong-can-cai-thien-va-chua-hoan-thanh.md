# RETRACK — việc cần cải thiện và chưa hoàn thành

Ngày lập: 17/11/2026. Căn cứ: danh sách ưu tiên của nhóm (`uu-tien-truoc-bao-ve-thang-12-2026.md`, rà theo `develop 633dddc`) và đối chiếu source ngày 17/11/2026.

Ký hiệu cột **Xác minh**:
- **Đã xác nhận trong source**: đã đọc code hoặc chạy lệnh để đối chiếu.
- **Theo danh sách nhóm**: lấy từ tài liệu ưu tiên, chưa kiểm tra lại bằng source.
- **Chưa kiểm được**: cần thiết bị, dịch vụ ngoài hoặc môi trường chưa có.

Tài liệu này là danh sách còn việc, không phải chứng nhận hệ thống hoàn hảo hoặc nghiệm thu production.

---

## A. Cần chốt trước bảo vệ (đầu tháng 12/2026)

| # | Việc | Người chủ trì | Xác minh | Chi tiết |
|---|---|---|---|---|
| A1 | Đồng bộ chính sách phí mới/cũ giữa DB và client | TV2, cả nhóm | Theo danh sách nhóm | Đọc bàn giao 15/11. Không tự tính phí ở từng role, không hồi tố đơn cũ. |
| A2 | Hóa đơn phí phát sinh giữa kỳ | TV5 | **Đã xác nhận trong source** | `AdminService.GenerateMonthlyInvoicesAsync` (dòng ~462-497) gặp payer/kỳ đã có hóa đơn thì `continue`. Phí phát sinh sau khi phát hành sẽ không vào hóa đơn nào. Cần khóa kỳ đã đóng hoặc làm hóa đơn điều chỉnh bổ sung, không sửa hóa đơn đã nộp. Test hiện chỉ kiểm tra việc bỏ qua kỳ đã có. |
| A3 | Mobile typecheck lỗi | TV1, TV3 hỗ trợ | **Đã xác nhận trong source** | `npm run typecheck` báo đúng 4 lỗi: `SellerNavigator.jsx` dòng 17 và 41 (thiếu prop `id` ở navigator), dòng 29 (gán chuỗi cho tên icon), `SellerHomeScreen.jsx` dòng 22 (route `CreateRequest` chưa khai báo kiểu). Đều là lỗi kiểu, có sẵn trên `develop`. |
| A4 | PayOS thật, đối soát mất callback/retry | TV2, TV5 | Một phần | Có `PayOsReconciliationWorker` (poll mỗi 5-600 giây, thử lại khi lỗi, tự tắt nếu chưa có key). Chưa có bằng chứng chạy với PayOS thật hoặc webhook HTTPS. |
| A5 | GPS, ca/tải xe, hủy chuyến/ngoại lệ giao nhận | TV3, TV2/TV4 | Một phần | `DriverDeliveryService` có kiểm ảnh, tọa độ, khoảng cách và các hành động cancel/reject/incident. Cần thử trên thiết bị thật. Chưa chốt việc trả hàng QC bị từ chối trước khi cộng tồn. |
| A6 | Phí/VAT dịch vụ và chủ thể vận hành | Cả nhóm, TV5 | **Đã xác nhận trong source** | Source chỉ có VAT đầu ra của lô và báo cáo kho. Hóa đơn nền tảng chỉ lưu `TotalFeeAmount`, không có logic VAT cho phí dịch vụ. Chưa chốt chủ thể thu phí và xuất hóa đơn. Không gọi chứng từ nội bộ là hóa đơn thuế hợp pháp. |
| A7 | Bộ kịch bản demo liên role dùng chung dữ liệu | TV1-TV5 | Một phần | Có seed trong `db/bootstrap/retrack-system.sql`. Cần chạy và chốt từng luồng: Seller-Employee-Depot, Depot-Driver-Factory, lô lớn giao nhận rồi nhập kho 1 lần, đồ cũ ghi bán ngoài, hồ sơ Owner-Admin. Xem mục E về khoảng trống dữ liệu seed. |
| A8 | Đồng bộ usecase/workflow/test report với source | TV2 tổng hợp | Theo danh sách nhóm | Phân biệt tính năng đã test thật, fixture, giả lập và chưa triển khai. Không báo toàn hệ thống 100%. |

---

## B. Cần trước khi deploy cho người dùng thật

| # | Việc | Người chủ trì | Xác minh | Chi tiết |
|---|---|---|---|---|
| B1 | Chọn nơi deploy, HTTPS, dung lượng DB và ngân sách | TV5 + nhóm | **Đã xác nhận** | Hiện có một Azure VM (`retrack-api-se44`, B2als v2, 4 GiB, Japan East) chạy backend qua HTTP:8080, dựng thủ công. Chưa có HTTPS. Backend công khai đang bật Swagger, tức chạy chế độ Development. |
| B2 | Nâng runtime .NET (hiện `net8.0`), cập nhật PostgreSQL minor | TV5 | **Đã xác nhận** | Phải thử trên staging, không nâng chen vào task phí. |
| B3 | Scan hồ sơ bằng ClamAV thật | TV5, TV2 | **Đã xác nhận** | Có `VerificationFileScanner`, mặc định `Mode=Disabled`. Chế độ này chỉ chạy được ở Development, Production chưa bật ClamAV sẽ từ chối nộp hồ sơ mới. Chưa có cấu hình hoặc Docker cho daemon trong repo. |
| B4 | Retention, audit ai xem/tải hồ sơ, CDR | TV5 + TV2 | **Đã xác nhận** | Có endpoint tải `documents/{id}` (có rate limit) nhưng không có bảng hoặc service ghi nhật ký truy cập, chính sách lưu/xóa hay CDR. |
| B5 | Backup DB và tệp cloud, thử restore | TV5 | **Đã xác nhận** | `db/` chỉ có `bootstrap` và README, không có script backup/restore. `pg_dump` không chứa byte hồ sơ lưu trên cloud. |
| B6 | DB quyền tối thiểu, TLS, pool, giám sát | TV5 | Theo danh sách nhóm | Giám sát query chậm, lock, dung lượng. Chọn hosting trước rồi đặt ngân sách. |
| B7 | Kiểm thử tải và quan sát lỗi | TV5, từng role | **Đã xác nhận** | Không có health check trước 17/11 (đã thêm `/health/live`), không có middleware request ID. Chưa có kiểm thử tải. |
| B8 | Dịch vụ AI nhận diện rác | TV1, TV5 | **Đã xác nhận** | Dịch vụ Python (PyTorch, FastAPI, cổng 5005) có `Dockerfile` nhưng chưa có CD. Backend gọi qua `AiService:Url`. Không chạy thì tính năng AI của Seller báo `offline`. PyTorch cần 1-2 GB RAM, đáng kể trên VPS nhỏ. |

Ảnh lô đã dùng Cloudinary. Hồ sơ xác minh mới hỗ trợ Cloudinary private từ 14/11. Hồ sơ DB cũ vẫn đọc được, chưa migrate dữ liệu cũ và chưa có job dọn orphan tự động.

---

## C. Hạ tầng CI/CD backend (làm ngày 17/11/2026)

| Hạng mục | Trạng thái |
|---|---|
| CI backend (`.github/workflows/backend-ci.yml`): build, test PostgreSQL, build Docker | Đã có, xanh trên PR #45 sau khi sửa lỗi dọn dữ liệu test |
| CD backend (`.github/workflows/backend-cd.yml`): build, đẩy image GHCR, SSH vào VM chạy container, kiểm tra health, tự quay về bản cũ | Đã có, **deploy chưa thành công lần nào** |
| Endpoint `/health/live` | Đã thêm vào `Program.cs` |
| Secret `AZURE_VM_HOST`, `AZURE_VM_USER`, `AZURE_VM_SSH_KEY` trong GitHub Environment `production` | **Chưa có** (job deploy báo `missing server host`) |
| Docker, `/opt/retrack/api.env`, thư mục `uploads`, khóa SSH của CI trên VM | **Chưa làm** |
| Quyền Azure cho tài khoản nhóm | Tài khoản `nguyenlanviet224` bị 403 trên VM, cần chủ subscription gán quyền (IAM) |
| CD cho dịch vụ AI | Chưa có |
| HTTPS, branch protection bắt CI xanh trước khi merge `main` | Chưa có |
| Credit Azure for Students ($100, hạn 12 tháng) | VM B2als v2 chạy 24/7 ước tính $35-40/tháng (chưa kiểm chứng với bảng giá Japan East). Hết credit thì subscription bị khóa. Cần kiểm tra Cost Management và đặt cảnh báo ngân sách |

---

## D. Chất lượng kiểm thử và tài liệu

| Việc | Ghi chú |
|---|---|
| Report 5 (Test Report) | 1000 test case: 821 Passed, 10 Failed, 169 Pending. Cần xử lý Failed và Pending trước khi nộp. |
| Report 5 (Unit Test) | 468 test case: 458 Passed, 6 Failed, 4 Untested. |
| Test dựa vào cách dựng DB | Test Retrack.Tests đã vỡ trên CI vì schema SQL không có `ON DELETE CASCADE` ở `batch_quality_checks` và `factory_depot_reviews`, nhưng test dọn dữ liệu không xóa trước. Đã sửa trong `InventoryBatchTests.DisposeAsync`. Cần xem các test khác có cùng giả định. |
| Fix nút đấu giá (`LiquidationDetail.jsx`) | Đã đưa vào PR #45. Mới kiểm tra bằng lint và build, chưa test lại trên trình duyệt sau lần sửa cuối. |
| Máy local không có .NET SDK | Backend chỉ được build và test thật trên CI, không build được trên máy này. |

---

## E. Dữ liệu seed (`db/bootstrap/retrack-system.sql`)

Seed đã nạp đủ cho 6 role (Admin, Seller, Depot Owner, Employee, Driver, Factory), kho, nhà máy, đơn thu gom, chuyến vận chuyển, tồn kho, phí, hóa đơn, thông báo, bảng giá và một lô thanh lý.

Các bảng **không có câu lệnh seed** nào (đối chiếu bằng cách so danh sách `CREATE TABLE` với `INSERT` sau `-- RETRACK_SCHEMA_END`):

| Bảng | Nhận xét |
|---|---|
| `liquidation_bids`, `liquidation_agreements` | Có lô thanh lý và một lượt tham gia nhưng chưa có lượt đấu giá hay thỏa thuận. Demo đấu giá phải tạo tay. |
| `depot_verification_documents`, `depot_verification_submissions` | Chưa có hồ sơ xác minh mẫu cho luồng Owner-Admin. Hợp lý vì có file, nhưng cần dữ liệu để demo. |
| `depot_batch_cost_sources`, `depot_batch_expenses`, `depot_cost_reconciliations`, `depot_stock_cost_adjustments`, `depot_reuse_sales`, `depot_incident_followups` | Chưa có dữ liệu mẫu cho giá vốn, đối soát, bán đồ cũ, sự cố. |
| `driver_delivery_events`, `employee_dispatch_events`, `employee_dispatch_queue` | Bảng sự kiện/hàng đợi sinh khi chạy luồng, không bắt buộc seed. |
| `password_reset_otps` | Sinh khi dùng, không cần seed. |
| `system_configs` | Bảng được `INSERT` ở phần schema (dòng 35) nên không phải thiếu. |

Số bản ghi thực tế trong mỗi bảng chưa được đếm, vì bảng seed nhiều dòng bằng `generate_series`.

---

## F. Thứ tự đề xuất

1. Đồng bộ phí và sửa hóa đơn giữa kỳ (A1, A2).
2. Sửa 4 lỗi typecheck mobile (A3).
3. Chốt kịch bản demo liên role, bổ sung seed còn thiếu cho luồng đấu giá và hồ sơ xác minh (A7, E).
4. Hoàn tất CD: quyền Azure, secret, cài Docker và `api.env` trên VM, HTTPS (C, B1).
5. Xử lý Failed/Pending trong Report 5 (D).
6. Quyết định dịch vụ AI chạy ở đâu và có đưa vào CD không (B8).

Điều chỉnh theo ngày bảo vệ thật. Không coi các việc phụ thuộc là đã xong.
