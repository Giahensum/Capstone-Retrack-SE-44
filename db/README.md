# Khởi tạo database ReTrack bằng một file SQL

## Hồ sơ xác minh kho (bổ sung 19/10/2026)

- Kho mới mặc định `UNVERIFIED`. Chủ kho tạo kho và nộp 1–3 hồ sơ PDF/PNG/JPEG (tối đa 2 MB mỗi tệp) trong Hồ sơ kho vựa; Admin đối soát tại Hồ sơ kho vựa trên giao diện Admin.
- Chưa duyệt: không tạo lô xuất, bán đồ cũ hoặc nhận giao dịch thu gom mới. Các đơn đang xử lý, thanh toán và đối soát nghĩa vụ cũ vẫn được tiếp tục. Thay đổi tên/địa chỉ/mã số thuế sau duyệt cần xác minh lại.
- File hồ sơ lưu riêng tư trong database, chỉ Owner tương ứng và Admin đang hoạt động được tải; không phải URL công khai. Cần đưa dữ liệu này vào chính sách backup/quyền truy cập DB của nhóm.
- Chỉ ba kho demo trong khối seed được đánh dấu đã duyệt. Không tự duyệt các kho thật khi nâng cấp. SQL bổ sung nằm trước `-- RETRACK_SCHEMA_END`; review và chạy phần schema cần thiết trên DB cũ, không chạy lại seed để nâng cấp dữ liệu thật.

## Máy mới: mở một file và nhấn F5

1. Trong pgAdmin, tạo database **rỗng**, ví dụ `Retrack_DB`.
2. Nhấp phải đúng database → **Query Tool**.
3. Chọn **Open File**, mở `db/bootstrap/retrack-system.sql`.
4. Bỏ vùng chọn nếu đang chọn một đoạn, rồi nhấn **F5** chạy toàn bộ file một lần.
5. Khi có `COMMIT`/`Query returned successfully`, Refresh **Schemas → public → Tables**.
6. Trỏ backend tới đúng database qua `.env` local; khởi động backend như README dự án.

Không cần terminal, không cần chạy file seed thứ hai hoặc `dotnet ef database update`. Ứng dụng không tự migrate. File SQL không đổi tài khoản/mật khẩu PostgreSQL và không chứa token Cloudinary/PayOS/JWT.

## Dữ liệu demo đi kèm

Mặc định đầu file có:

```sql
SET LOCAL retrack.seed_demo = 'on';
```

Giữ `on` để có dữ liệu test dùng chung ở Đà Nẵng cho Admin, Seller, Depot Owner Ngô Sỹ Giá, Employee, Driver và Factory. File gồm13tài khoản,3kho,3nhà máy, đơn thu gom nhiều trạng thái, lô xuất/nhu cầu/quan hệ hợp tác/KCS, chuyến vận chuyển, giao dịch phí/hóa đơn/thông báo/bảng giá và phiếu nhập hỗn hợp sắt+ghế cùng chi phí công/xăng xe. Địa chỉ demo ở Ngũ Hành Sơn, Hải Châu, Cẩm Lệ; dữ liệu có nhãn `[DEMO]`.

Nếu chỉ cần cấu trúc và cấu hình mặc định, đổi **chính dòng đó** thành:

```sql
SET LOCAL retrack.seed_demo = 'off';
```

Khi tắt seed, không tạo tài khoản/đơn/giao dịch demo; các cấu hình hệ thống mặc định vẫn cần để API hoạt động. Không bật seed demo trên môi trường thật. Nếu đã nạp bằng SQL, giữ `Database__Initialize=false` trong cấu hình backend để không cần bộ seed C# bổ sung. Mật khẩu demo chỉ lưu dạng BCrypt hash; không sử dụng các tài khoản demo trong môi trường thật.

## Chạy lại và xử lý lỗi

File dùng một transaction cho cả schema và seed. Chạy lại trên database demo cùng cấu trúc không nhân đôi UUID fixture và không đặt lại trạng thái đơn đã thao tác. Email/ID/role demo xung đột sẽ báo lỗi thay vì nối nhầm tài khoản.

Nếu có lỗi, dữ liệu của lượt chạy đó chưa được ghi. Trong cùng Query Tool, chạy `ROLLBACK;`, đọc dòng `ERROR` đầu tiên, sửa nguyên nhân rồi chạy lại toàn bộ file. `NOTICE ... already exists, skipping` là thông báo bỏ qua bảng/cột đã có, không phải lỗi. Không xóa/reset database để thử sửa lỗi.

## Database đã có dữ liệu nghiệp vụ

Bootstrap dành cho database mới/demo; tính chạy lại không biến nó thành công cụ nâng cấp mọi phiên bản database cũ. Trước nâng cấp: sao lưu, đối chiếu cột/bảng thực tế và kiểm `SELECT current_database();` có đúng DB backend đang dùng.

Riêng bổ sung nhập kho đa nguồn cho database đã có nền schema: mở cùng `bootstrap/retrack-system.sql`, chọn **từ `-- RETRACK_DEPOT_STOCK_V1` đến trước `-- RETRACK_SCHEMA_END`**. Đọc/review khối này rồi chạy trong transaction riêng. Không chọn sang `-- RETRACK_SEED_BEGIN` và không chạy seed demo trên dữ liệu thật. Khối này không nhập lại pickup `DONE` hoặc coi lô bị QC từ chối là hàng đã quay về.

Các script trong `db/employee`, `db/driver`, `db/factory` là bản vá/fixture riêng cho database cũ, **không cần chạy khi setup máy mới**. Giữ chúng vì có mục đích riêng của các role, không coi là file khởi tạo hệ thống thứ hai. `cleanup_legacy_pascalcase_tables.sql`/`inspect_legacy_pascalcase_tables.sql` chỉ dùng sau khi đã review nhu cầu chuyển dữ liệu PascalCase cũ.

## Khi nhóm thêm bảng/cột

Cập nhật schema vào **cùng** `bootstrap/retrack-system.sql`, trước marker `-- RETRACK_SCHEMA_END`. Seed chung đặt trong khối `RETRACK_SEED_BEGIN`/`RETRACK_SEED_END`. Không thêm nguồn schema hoặc file seed bootstrap song song.

Marker giúp test và người nâng cấp chọn đúng phần, không phải file thứ hai. Báo trong PR các thay đổi model, SQL, seed và cách nâng cấp database đã có dữ liệu; không sửa migration EF đã chia sẻ hoặc áp EF migration chồng lên DB-first.

Hai file schema/seed cũ đã được gộp và xóa; không cần tìm lại chúng. Lịch sử vẫn có trong Git. Cấu hình PostgreSQL/appsettings/.env riêng của từng thành viên được giữ nguyên.

Khối schema mới cũng có hồ sơ xác minh private, lịch sử điều chỉnh/đối soát vốn, theo dõi sự cố Depot và contract lô lớn. Kho thật mặc định UNVERIFIED, cần nộp hồ sơ và Admin duyệt để mở giao dịch mới; chỉ ba kho demo được duyệt trong seed. Seed có hai lô lớn `[DEMO]` để thử màn Depot; không thay cho API đăng lô Seller của TV1. Máy cũ cần bổ sung toàn khối mới trước khi chạy bản backend tương ứng; chỉ `dotnet run` không tự tạo cột/bảng.
