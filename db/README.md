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

## Dữ liệu demo đã gộp

Mặc định file có `SET LOCAL retrack.seed_demo = 'on';` để nạp:

| Nhóm | Dữ liệu |
|---|---|
| Tài khoản | 26 tài khoản, đủ 6 role; Chủ kho demo Ngô Sỹ Giá. Giữ các email chính admin/seller/depot/employee/driver/factory@retrack.vn và Factory2/3 |
| Kho/nhà máy | 5 kho, 4 nhà máy, 15 liên kết nhân sự; địa chỉ mô phỏng Đà Nẵng |
| Thu gom | 65 đơn nhiều trạng thái, gồm 13 đơn Employee `e0000000-0929-*` và 36 đơn điều phối `ed250000-*` |
| Điều phối | 6 nhân viên, 6 Seller, 6 vùng và ca demo khoảng 20 giờ; đơn hẹn ngày sau chưa được giao từ hôm nay |
| Vận chuyển | 12 chuyến, gồm 3 lô `DRV-DEMO-01/02/03` và 3 lô `DRV-DN-LOCAL-01/02/03`, giữ UUID cũ để mobile tiếp tục test |
| Tồn/chi phí | Phiếu sắt+ghế cùng chi phí; thêm 900 kg PET nguồn demo cho hai bộ lô Driver, giá vốn chưa xác định |
| Liên role | Hợp tác, nhu cầu, lô xuất/cân/KCS, thanh toán/phí/hóa đơn, thông báo, bảng giá và lô thanh lý |

- `employee.dispatch01@retrack.test`…`employee.dispatch06@retrack.test` và Seller dispatch sao chép hash từ tài khoản Employee/Seller nguồn khi tạo lần đầu. `factory.driver-local@retrack.test` sao chép hash Factory nguồn khi tạo lần đầu; không đổi mật khẩu tài khoản đã có.
- Lô Driver tại chỗ dùng điểm `X7P4+XF9 Ngũ Hành Sơn, Đà Nẵng`. Kho và nhà máy cùng tọa độ chỉ để thử camera/giao nhận tại chỗ, không dùng đánh giá thuật toán tuyến đường.
- Dữ liệu chỉ mô phỏng, không phải yêu cầu thu gom/giao hàng/thanh toán thật. Nguồn Driver chưa khai giá vốn không được coi vốn bằng 0 để báo lãi.
- Ca demo hết giờ sau khoảng 20 giờ. Hôm khác Owner tạo ca mới ở Điều phối thu gom; chạy lại file không bật ca STOPPED/FULL hoặc kéo dài ca đã dùng.

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
