# Database ReTrack — chỉ chạy một file SQL

**File duy nhất: [`bootstrap/retrack-system.sql`](bootstrap/retrack-system.sql).** Gồm schema toàn hệ thống và seed chung cho Admin, Seller, Depot Owner, Employee, Driver, Factory. Không cần chạy thêm SQL theo role.

## Máy mới: chạy thủ công trong pgAdmin

1. Tạo database mới, rỗng, ví dụ `Retrack_DB`.
2. Nhấp phải đúng database → Query Tool → Open File.
3. Mở `db/bootstrap/retrack-system.sql` từ source vừa pull.
4. Bỏ vùng chọn nếu đang chọn một đoạn; nhấn F5 chạy toàn bộ file.
5. Khi thấy COMMIT/Query returned successfully, Refresh Schemas → public → Tables.
6. Trỏ backend tới DB đó bằng `ConnectionStrings__DefaultConnection` trong `backend/Retrack.API/.env` local. Giữ tài khoản/mật khẩu PostgreSQL của máy mình.
7. Giữ `Database__Initialize=false`, rồi chạy backend theo README dự án.

Không cần terminal để dựng DB, không cần file seed thứ hai hoặc `dotnet ef database update`. Backend không tự migrate khi `dotnet run`.

## Cập nhật 25/11/2026: gói quản lý Depot và thanh toán trực tiếp

- Cùng file SQL trên bổ sung hai bảng `depot_subscriptions`, `depot_subscription_orders`, chỉ mục và cột ghi hình thức thanh toán trực tiếp. Không drop bảng, không sửa số tiền/phí đã chốt của đơn cũ.
- Mặc định `BUSINESS_MODEL=DEPOT_SUBSCRIPTION`: chỉ bán gói cho Depot, 199.000đ/kho/tháng lịch, giá thay đổi được tại Admin → Gói dịch vụ. Giao dịch mới không tính hoa hồng; các role còn lại không mua gói.
- Owner chọn từng kho → Gói dịch vụ → **Dùng thử 14 ngày** một lần cho kho đã xác minh, hoặc mua/gia hạn. Chạy SQL lại không bắt đầu/kéo dài dùng thử. Mua gói không thay cho việc Admin duyệt hồ sơ kho.
- DB cũ: sao lưu, dừng backend và áp phần schema của file, kết thúc trước `-- RETRACK_SCHEMA_END`, thêm `COMMIT;`; không chạy seed. Giữ nguyên `.env` và tài khoản PostgreSQL của máy mình. Sau đó khởi động lại backend và tải lại frontend.
- Hai bên tự thanh toán tiền hàng: chuyển khoản kèm chứng từ, tiền mặt hoặc hình thức khác có căn cứ; bên nhận vẫn xác nhận tiền thực nhận. PayOS mới chỉ dùng để mua gói Depot; khoản thu/chi/hoàn cũ vẫn được đối soát theo dữ liệu đã có.
- PayOS dùng cấu hình local `PayOS__*` hiện có, webhook chung `/api/payos/webhook`. Server xác minh chữ ký/số tiền hoặc truy vấn provider; URL quay về không tự kích hoạt gói. Có nút kiểm tra và tác vụ đối soát lại. Không bật thanh toán mô phỏng thành tiền thật.
- Chuyển khoản mua gói thủ công: Admin cấu hình tài khoản nhận tiền, Owner gửi chứng từ; Admin kiểm tra thực nhận rồi kích hoạt hoặc yêu cầu bổ sung. Phiếu mua gói chưa thay thế hóa đơn điện tử hợp pháp.

## Dữ liệu demo đã gộp

Bổ sung kế hoạch tự động theo ngày: 12 đơn, 12 Seller và 8 Employee ở hai kho mới không có vùng/ca seed. Worker tự chuẩn bị danh mục và phân công; xem [AUTO_DAILY_COLLECTION.md](../docs/AUTO_DAILY_COLLECTION.md). DB cũ cần chạy riêng khối `RETRACK_AUTO_DISPATCH_DAY_V1_BEGIN/END` trước backend mới, không chạy seed trên production. Bộ 6 Employee cũ vẫn giữ để đối chiếu, bộ mới dùng `employee.dispatch09` đến `employee.dispatch16`.

Mặc định file có `SET LOCAL retrack.seed_demo = 'on';` để nạp:

| Nhóm | Dữ liệu |
|---|---|
| Tài khoản | 52 tài khoản: 1 Admin, 1 Depot Owner, 16 Employee (14 hoạt động), 2 Driver, 4 Factory, 28 Seller. Tên và mô tả tự nhiên, dữ liệu chỉ dùng demo |
| Kho/nhà máy | 8 kho, 4 nhà máy, 23 liên kết nhân sự; thêm Thành Đạt và Minh Phát để test tự lập ca |
| Thu gom | 40 đơn gồm dữ liệu lịch sử liên role, 12 đơn dispatch tại địa điểm công cộng và 12 đơn bổ sung gọn cho hai kho mới |
| Điều phối | dispatch01–03 thuộc An Phú, dispatch04–06 thuộc Hòa Bình. dispatch09–12 thuộc Thành Đạt, dispatch13–16 thuộc Minh Phát; hai kho mới không seed ca/vùng, worker tự lập. Đơn hẹn ngày sau chờ đúng ngày |
| Vận chuyển | 12 chuyến, gồm 3 lô `DRV-DEMO-01/02/03` và 3 lô `DRV-DN-LOCAL-01/02/03`, giữ UUID cũ để mobile tiếp tục test |
| Tồn/chi phí | Phiếu sắt+ghế cùng chi phí; thêm 900 kg PET nguồn demo cho hai bộ lô Driver, giá vốn chưa xác định |
| Liên role | Hợp tác, nhu cầu, lô xuất/cân/KCS, thanh toán/phí/hóa đơn, thông báo, bảng giá và lô thanh lý |

- `employee.dispatch01@retrack.test`…`employee.dispatch06@retrack.test` và Seller dispatch sao chép hash từ tài khoản Employee/Seller nguồn khi tạo lần đầu. `factory.driver-local@retrack.test` sao chép hash Factory nguồn khi tạo lần đầu; không đổi mật khẩu tài khoản đã có.
- Lô Driver tại chỗ dùng điểm `X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng`. Kho và nhà máy cùng tọa độ chỉ để thử camera/giao nhận tại chỗ, không dùng đánh giá thuật toán tuyến đường.
- Dữ liệu chỉ mô phỏng, không phải yêu cầu thu gom/giao hàng/thanh toán thật. Nguồn Driver chưa khai giá vốn không được coi vốn bằng 0 để báo lãi.
- Ca demo hết giờ sau khoảng 20 giờ. Hôm khác Owner tạo ca mới ở Điều phối thu gom; chạy lại file không bật ca STOPPED/FULL hoặc kéo dài ca đã dùng.
- Hướng dẫn tài khoản, địa điểm, chuyển đơn trong cùng kho và tạo đơn bất chợt: [Kịch bản điều phối Employee](../docs/EMPLOYEE_DISPATCH_DEMO.md).
- `employee.dispatch07@retrack.test` và `employee.dispatch08@retrack.test` là hai hồ sơ lịch sử **bị khóa**, không dùng đăng nhập. Không xóa vật lý vì các đơn cân/thanh toán cũ còn tham chiếu ID. Bộ mới không tạo email `employee@retrack.vn`, `employee2@retrack.vn` hoặc `dispatch.v2.*`.
- DB đã seed phiên bản trước không tự mất fixture cũ khi pull code. Bộ mới dùng UUID riêng để không ghi đè lịch sử; nếu cần demo sạch, dựng một **database test mới** rồi trỏ backend vào đó, giữ DB cũ để đối chiếu.

## Chỉ lấy schema

Đổi chính dòng đầu file thành `SET LOCAL retrack.seed_demo = 'off';`, rồi chạy toàn bộ. Tất cả bộ demo cùng tuân theo tùy chọn này. Cấu hình hệ thống mặc định vẫn được tạo để API hoạt động; không tạo tài khoản/đơn demo. Không bật seed demo ở production.

## Chạy lại và lỗi thường gặp

Schema và seed nằm trong một transaction. Chạy lại trên DB demo cùng cấu trúc không nhân đôi UUID, reset đơn/chuyến đã xử lý, thay mật khẩu hoặc mở lại ca đã ngừng. Không có tùy chọn reset dữ liệu trong file chung. Dừng backend trước khi chạy lại trên các đơn demo đang dùng.

- `NOTICE ... already exists, skipping`: đối tượng đã có, không phải lỗi.
- Nếu có ERROR, đọc lỗi đầu tiên; chạy `ROLLBACK;` trong cùng Query Tool, sửa nguyên nhân rồi thử lại. Không xóa database để sửa lỗi.
- Xung đột ID/email/role hoặc fixture dở dang sẽ dừng transaction, không ghi đè dữ liệu để ép seed thành công.
- Upload/PayOS/Google/Goong cần cấu hình riêng. SQL không chứa token hoặc mật khẩu kết nối PostgreSQL.

## DB cũ đã có dữ liệu

Pull code không tự sửa PostgreSQL. Sao lưu, kiểm tra `SELECT current_database();` và đối chiếu schema trước khi nâng cấp. Dùng cùng file SQL để chọn khối schema cần thiết, kết thúc trước `-- RETRACK_SCHEMA_END`, chạy trong transaction riêng. Không chọn sang `-- RETRACK_SEED_BEGIN` khi nâng cấp dữ liệu thật.

Marker chính: `RETRACK_DEPOT_STOCK_V1`, `RETRACK_LIQUIDATION_DEPOSIT_V1`, `RETRACK_DEPOT_NOTIFICATIONS_V1`, `RETRACK_EMPLOYEE_DISPATCH_BEGIN/END`. CREATE TABLE IF NOT EXISTS không tự sửa mọi cột/kiểu/bảng legacy; bootstrap không chuyển đổi mọi DB lịch sử. Không áp EF migration chồng lên SQL để thử hết lỗi.

Các SQL cũ đã gộp/xóa khỏi Git. Script kiểm kê/dọn PascalCase lưu local trong `.ai-context`, không chạy kèm bootstrap hoặc tự xóa bảng legacy. Tra lịch sử Git trước commit hợp nhất khi cần đối chiếu. Tệp Factory trên ổ đĩa vẫn phải lưu bền vững và sao lưu cùng DB; SQL không chứa nội dung tệp.

## Khi nhóm thêm bảng/cột hoặc seed

### Cập nhật 17/11/2026: lô Factory, trả hàng và hóa đơn cuối kỳ

Máy mới vẫn chạy toàn bộ **một file SQL duy nhất** như hướng dẫn đầu trang.
DB đã có dữ liệu cần sao lưu và dừng backend trước khi nâng; không chạy seed.
Trong Query Tool của đúng database, chạy các khối sau trong một transaction
riêng, bao bởi BEGIN; và COMMIT;:

- RETRACK_CLOSED_PERIOD_BILLING_V1 đến RETRACK_CLOSED_PERIOD_BILLING_V1_END:
  revision/liên kết hóa đơn bổ sung và unique index theo lần phát hành.
- RETRACK_BATCH_SETTLEMENT_APPROVAL_V1 đến RETRACK_BATCH_SETTLEMENT_APPROVAL_V1_END:
  phiên bản thỏa thuận giá, xác nhận Depot và bảng lịch sử.
- RETRACK_DEMAND_ORIGIN_V1 đến RETRACK_DEMAND_ORIGIN_V1_END:
  liên kết lô với nhu cầu Factory.
- RETRACK_BATCH_QUALITY_RESOLUTION_V1_BEGIN đến RETRACK_BATCH_QUALITY_RESOLUTION_V1_END:
  chứng từ bàn giao trả và xác nhận nhận đủ.

Nếu chưa nâng các cập nhật trước, cần đối chiếu toàn bộ phần schema trước
RETRACK_SCHEMA_END; các khối trên không thay thế lịch sử nâng DB.
Develop cũng có thêm các cột esign_* trong factory_depot_partnerships cho
DocuSign; cần giữ phần này khi nâng schema từ bản cũ. Chưa cấu hình DocuSign
thì ký điện tử chưa sẵn sàng; không ảnh hưởng đăng nhập hoặc nhập kho.

Lô cũ giữ settlement_workflow_version=0. Lô mới tạo qua API dùng phiên bản 1:
Factory đề xuất giá → Depot đồng ý → Factory gửi chứng từ → Depot xác nhận
nhận đủ tiền mới PAID. Chỉ bước cuối ghi nhận tiền/phí của lô mới.
QC bị từ chối chưa có nghĩa hàng đã về; chỉ RETURNED sau xác nhận hai phía
mới hoàn nguồn tồn. Không cập nhật hàng loạt trạng thái cũ để ép theo luồng mới.
Backend không tự chạy các khối SQL khi dotnet run.

Bổ sung 14/11/2026: hồ sơ kho có phân loại giấy tờ kinh doanh, ảnh kho và tài liệu
bổ sung. DB cũ cần chạy khối
`RETRACK_DEPOT_VERIFICATION_DOCUMENTS_V2_BEGIN/END` trong chính file SQL chung
trước khi chạy backend mới. Khối này chỉ thêm cột `kind`, hồ sơ cũ giữ
`LEGACY`; không sửa nội dung tệp, trạng thái duyệt hoặc chạy seed.
Ảnh kho là tài liệu hỗ trợ tùy chọn, không thay cho giấy tờ kinh doanh.

Bổ sung kiểm tra an toàn tệp 14/11/2026:
`RETRACK_DEPOT_VERIFICATION_SCANNING_V1_BEGIN/END` thêm `scan_status` và
`scanned_at` vào tài liệu xác minh. DB cũ cần chạy khối này trước backend mới.
Hồ sơ cũ giữ `NOT_SCANNED`, không đổi thành sạch và không mất nội dung.
Mọi bổ sung vẫn nằm trong chính một file `bootstrap/retrack-system.sql`.
Chi tiết cấu hình scanner ở mục “Bảo vệ hồ sơ Depot” trong README gốc.

Bổ sung cloud riêng tư: `RETRACK_DEPOT_PRIVATE_CLOUD_V1_BEGIN/END` thêm provider,
key, kích thước và SHA-256 vào metadata hồ sơ; điều chỉnh CHECK để cloud có
byte DB rỗng và đủ metadata, còn hồ sơ DATABASE vẫn phải có nội dung hợp lệ.
Không chuyển/xóa nội dung hồ sơ cũ. Máy đã nâng đến af6446a chỉ cần sao lưu
rồi chạy khối này trước backend mới, không chọn seed. Máy mới chạy một file
chung như trên. Cấu hình cloud dùng account Cloudinary hiện có; xem README gốc.

## Chính sách bên mua trả phí riêng — 15/11/2026

DB đã có dữ liệu: sao lưu, mở đúng database trong pgAdmin, chọn và chạy trọn
khối `RETRACK_BUYER_FEES_V1_BEGIN` đến `RETRACK_BUYER_FEES_V1_END` trong
`bootstrap/retrack-system.sql`, sau đó khởi động lại backend. Không chọn seed,
không chạy EF migration. Nếu DB còn thiếu các cập nhật trước, nâng schema
theo các mục trên trước; khối này không thay thế toàn bộ lịch sử nâng DB.
Máy mới vẫn chỉ chạy toàn bộ một file SQL chung.

Khối mới thêm `fee_policy` vào `pickup_requests`, `inventory_batches`,
`platform_transactions` và `fee_percentage_snapshot` vào `inventory_batches`.
Không cập nhật lại tiền hay trạng thái cũ; chạy lại khối không đổi chính sách
đã chốt. Các đơn/lô demo lịch sử giữ chính sách cũ để kiểm thử tương thích.

- Đơn/lô tạo mới qua API: `BUYER_SEPARATE_V1`. Depot mua Seller thì Depot
  chịu phí riêng; Factory mua Depot thì Factory chịu phí riêng.
- Dữ liệu trước khi nâng: `LEGACY_SELLER_DEDUCTION_V1`, giữ số thực nhận đã
  chốt. Không tự sửa các đơn đang xử lý hoặc đã thanh toán sang công thức mới.
- Phí mới tính trên tiền hàng chưa VAT, làm tròn VND nguyên; không tính trên
  VAT hàng. Mức phí cấu hình chốt khi tạo đơn/lô mới, không đổi khi Admin
  chỉnh cấu hình sau đó. Cấu hình nhận tối đa hai chữ số thập phân.
- Ví dụ tiền hàng 1.000.000 đ, phí 5%: Seller nhận 1.000.000 đ; Depot trả
  nền tảng 50.000 đ riêng, tổng chi 1.050.000 đ. Phí mua này nằm trong giá vốn
  nguồn pickup mới, không được cộng lần thứ hai khi xuất hàng.
- Factory mua hàng 10.000.000 đ và VAT hàng theo hóa đơn 1.000.000 đ: Depot
  nhận 11.000.000 đ; Factory chịu phí 500.000 đ riêng, tổng chi 11.500.000 đ.
  Phí của Factory không bị trừ thêm khỏi doanh thu Depot.
- `NetAmount`/`NetPayableAmount` vẫn là tiền phải chuyển cho bên bán;
  `BuyerTotalAmount` là số đó cộng nghĩa vụ phí riêng với nền tảng.
  Các client phải đọc `FeePolicy` của đơn/lô, không tự suy từ cấu hình hiện tại.

Hóa đơn phí nền tảng vẫn thu riêng theo kỳ; một nguồn chỉ có một giao dịch
phí. Chưa tự đặt VAT dịch vụ nền tảng hoặc thêm phí vào tiền cọc/lô đấu giá,
bán đồ cũ ngoài hay gói dịch vụ. PayOS thật cần nghiệm thu riêng.

Khối `RETRACK_DEPOT_QUERY_INDEXES_V1_BEGIN/END` bổ sung ba index cho danh sách
lô theo kho/thời gian, lịch sử hồ sơ theo kho/thời gian và tệp theo hồ sơ.
Không đổi trạng thái, số tiền, tồn kho hoặc quyền của role khác.
EF chỉ mô tả index tương ứng; ứng dụng vẫn không tự migrate.

Bổ sung 08/11/2026: bảng giá thu mua riêng của từng kho nằm trong khối
`-- RETRACK_DEPOT_BUYING_PRICES_V1_BEGIN` đến `-- RETRACK_DEPOT_BUYING_PRICES_V1_END`
trong chính file SQL chung. DB cũ đã có schema khác đầy đủ chỉ cần chạy khối này
trong transaction sau khi kiểm tra đúng database; không chọn sang seed.
Không cần EF update. Máy mới vẫn chạy một file đầy đủ như trên.
Giá thị trường Admin và giá kho công bố là hai nguồn khác nhau; kho chưa công bố
thì Seller thấy thông báo chưa có giá, không lấy giá thị trường gán cho tất cả kho.

Chỉnh cùng `bootstrap/retrack-system.sql`: schema trước RETRACK_SCHEMA_END, seed giữa RETRACK_SEED_BEGIN/END. Mọi seed phải kiểm retrack.seed_demo, giữ UUID và trạng thái đã dùng. Không thêm bootstrap/seed theo role song song. Giữ các marker mà tests đang dùng; trước push kiểm bootstrap mới, schema-only, chạy lại và rollback khi lỗi.

## Triển khai có người dùng và theo dõi hiệu năng

Máy mới tiếp tục chạy một file bootstrap. Với DB cũ nhỏ, sao lưu và chọn đúng
hai khối nâng cấp ở trên trong giờ bảo trì, đặt `lock_timeout` ngắn để dừng nếu
đang có giao dịch giữ khóa. Không chạy lại seed để tối ưu DB.

Với DB đang có nhiều ghi, người phụ trách hạ tầng có thể tạo lần lượt ba index
dưới đây trong Query Tool bật Auto-commit, **ngoài BEGIN/COMMIT**. Không chạy
khối tạo index thông thường trong bootstrap đồng thời với thao tác này:

```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_inventory_batches_depot_newest
    ON inventory_batches(depot_id, created_at DESC, id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_depot_verification_history
    ON depot_verification_submissions(depot_id, submitted_at DESC, id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_depot_verification_documents_submission
    ON depot_verification_documents(submission_id);
```

Nếu thao tác bị ngắt, kiểm tra `pg_index.indisvalid` trước khi thử lại:
`IF NOT EXISTS` chỉ kiểm tra tên, không sửa index sai định nghĩa hoặc INVALID.
Không tự xóa index cũ vì có thể được role khác sử dụng.
[Tạo index PostgreSQL](https://www.postgresql.org/docs/17/sql-createindex.html).

Trước và sau mỗi đợt tối ưu, đo truy vấn thực bằng
`EXPLAIN (ANALYZE, BUFFERS)` trên dữ liệu đại diện; ghi cả thời gian, số dòng,
buffer và điều kiện lọc. ANALYZE thực thi truy vấn: dùng SELECT đã review.
Index mới ưu tiên trang đầu theo thời gian; tìm kiếm chứa chuỗi, đếm tổng,
trang OFFSET rất sâu và các báo cáo tổng hợp vẫn cần đo riêng.
[Đọc kế hoạch truy vấn](https://www.postgresql.org/docs/17/using-explain.html).

Người vận hành nên thiết lập các việc sau ở môi trường staging trước:

- Thu thập p50/p95/p99 HTTP, tỷ lệ 5xx, truy vấn chậm, lock wait, kết nối đang dùng,
  dung lượng DB/index và dung lượng sao lưu. Không ghi JWT, mật khẩu, nội dung
  giấy tờ hoặc tham số nhạy cảm vào log.
- Cân nhắc `pg_stat_statements` để xác định truy vấn tiêu tốn tổng thời gian;
  module có thể cần thay cấu hình server và restart, nên không tự bật từ
  bootstrap hay lúc ứng dụng khởi động.
  [Thống kê truy vấn](https://www.postgresql.org/docs/17/pgstatstatements.html).
- Theo dõi `n_dead_tup`, `last_autovacuum`, `last_autoanalyze` và thống kê
  từng bảng. Giữ autovacuum hoạt động; không chạy `VACUUM FULL` định kỳ trên
  DB đang phục vụ người dùng.
  [Bảo trì PostgreSQL](https://www.postgresql.org/docs/17/routine-vacuuming.html).
- Npgsql đã có connection pool. Tính tổng pool của mọi replica, worker và
  dịch vụ, chừa kết nối vận hành trong ngân sách PostgreSQL. Không tăng
  `Maximum Pool Size`, `max_connections` hoặc `work_mem` chỉ vì có thêm user;
  cần đo CPU/RAM, thời gian chờ và cấu hình máy chủ thực.
  [Cấu hình Npgsql](https://www.npgsql.org/doc/connection-string-parameters.html).
- Tách tài khoản chạy app khỏi tài khoản DDL/backup, dùng TLS xác minh
  chứng chỉ khi kết nối qua mạng; cấu hình secret trên nơi deploy.
- Kiểm thử khôi phục backup, thống nhất RPO/RTO, theo dõi WAL/dung lượng đĩa;
  quét malware cho hồ sơ trước khi mở dịch vụ công khai. Khi số tệp lớn,
  cân nhắc object storage riêng tư có kiểm quyền và thời hạn truy cập;
  không chuyển giấy tờ sang URL Cloudinary công khai.

Các mục hạ tầng này là hướng dẫn triển khai, chưa có số liệu tải thực hoặc
xác nhận vận hành trên máy chủ của nhóm. Không bật cache tồn kho/thanh toán,
replica đọc trễ, partition hay sharding khi chưa có nhu cầu đã đo và phương án
giữ tính nhất quán.


### Nâng cấp riêng phần vận chuyển thuê ngoài

Schema vận chuyển nằm trong `bootstrap/retrack-system.sql`, giữa `-- RETRACK_SHIPPING_BEGIN` và `-- RETRACK_SHIPPING_END`, trước `-- RETRACK_SCHEMA_END`. Không có file SQL Shipping riêng.

Với DB đang có dữ liệu: sao lưu, xác minh đúng database, chọn duy nhất nội dung giữa hai marker Shipping và chạy trong transaction `BEGIN;` … `COMMIT;`. Không chạy kèm phần seed hoặc thay toàn bộ database chỉ để thêm vận chuyển. Khối này bổ sung hai cột `shipping_method`, `shipping_quote_id` vào `inventory_batches` và ba bảng `shipping_quotes`, `external_shipments`, `shipping_events`. Lô cũ mặc định dùng `INTERNAL`.

Bài test `ShippingPostgresTests` chỉ đọc bootstrap chung, tắt seed rồi chạy lại khối Shipping từ chính nội dung đã đọc để kiểm tra tính lặp an toàn và đặt xe đồng thời. Khi đưa code lên Git cần giữ cả hai marker Shipping trong bootstrap.


### Bộ kiểm thử điều phối bảo vệ ngày 07/12/2026

Bootstrap hiện seed 10 Employee dispatch01–10 (5 người Thành Đạt, 5 người Minh Phát), 20 Seller và 160 đơn tại vị trí tra cứu Goong. Không seed ca hoặc phân công; worker tự tạo ca 07:00–19:00. Mỗi vùng có 16 đơn: 4 linh hoạt và 2 đơn cho mỗi khung 07–09, 09–11, 11–13, 13–15, 15–17, 17–19. Giữ nguyên ID 001–100, thêm 101–160. Đơn quá giờ/ngày phải chờ Owner hẹn lại, không tự dời ngày. Ngày demo cố định tại biến `demo_day` trong khối `RETRACK_AUTO_DISPATCH_BULK_SEED_BEGIN/END`; chạy lại file không reset lịch sử đã sử dụng.

Schema lịch mới nằm trong khối `RETRACK_PICKUP_SCHEDULE_V2_BEGIN/END` trước `RETRACK_SCHEMA_END`; DB cũ chỉ nâng schema sau backup, không seed chồng. Hướng dẫn test từng tình huống và cập nhật APK: [AUTO_DAILY_COLLECTION.md](../docs/AUTO_DAILY_COLLECTION.md).


Theo yêu cầu chuẩn bị bảo vệ, có tác vụ riêng `employee/prepare_defense_demo.sql` để nâng bộ 100 lên 160 và đổi lịch bộ demo trên DB demo đã có bootstrap mới; không phải bootstrap thứ hai. Chỉnh một dòng `SET LOCAL retrack.demo_date = '2026-12-07';`. File không reset đơn đã check-in/kết thúc, chỉ thêm 60 mã mở rộng còn thiếu, không tạo lại tài khoản/100 mã gốc. Upload, backup, chạy và kiểm tra theo [DEFENSE_DEMO_DEPLOY_GUIDE.md](../docs/DEFENSE_DEMO_DEPLOY_GUIDE.md). Cài APK/test local và chuyển HTTPS deploy xem [MOBILE_APK_LOCAL_AND_DEPLOY.md](../docs/MOBILE_APK_LOCAL_AND_DEPLOY.md).

Giữ file tác vụ này trong Git tại đúng đường dẫn `db/employee/prepare_defense_demo.sql`, kể cả khi chưa đến ngày sử dụng: `DefenseDemoPostgresTests` đọc file để kiểm thử trên DB test. Không cut ra ngoài repo. Có file trong Git không có nghĩa ứng dụng/CI/CD tự áp lịch lên DB deploy; bước đó vẫn chạy thủ công theo hướng dẫn.
