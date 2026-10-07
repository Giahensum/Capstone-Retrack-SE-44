# Diễn tập nâng cấp schema Factory

Chỉ chạy trên bản sao PostgreSQL dùng riêng để kiểm thử. Không áp dụng migration trực tiếp lên database dùng chung.

1. Khôi phục bản sao dữ liệu trước nâng cấp hoặc tạo database tạm tại migration cũ. Ghi lại số hàng của `users`, `factories`, `depots`, `inventory_batches` và các bảng giao dịch liên quan.
2. Đặt `ConnectionStrings__DefaultConnection` trỏ đến database tạm. Dùng `dotnet ef database update --project backend/Retrack.API --startup-project backend/Retrack.API` để áp dụng các migration EF sau khi đã review SQL.
3. Kiểm tra `__EFMigrationsHistory` và đối chiếu số hàng, khóa liên kết, trạng thái các lô cũ. Đọc lại một lô cũ bằng API Factory và Depot.
4. Chạy lại lệnh migration để kiểm tra tính lặp lại, rồi ghi log và kết quả đối chiếu vào hồ sơ kiểm chứng.

`Program.cs` hiện **không tự chạy migration khi khởi động**. `Database__Initialize=true` chỉ bật seed dữ liệu phát triển; không dùng tùy chọn này để nâng schema.

Ngày 05/10/2026 đã diễn tập trên PostgreSQL tạm UTF8: tạo schema `InitialCreate`, chèn 2 user, 1 Factory, 1 Depot và 1 lô `ACCEPTED`, sau đó nâng lên migration mới nhất. Lệnh nâng cấp thành công và số hàng/trạng thái lô cũ được giữ nguyên. Kết quả này chưa thay thế kiểm tra trên bản sao database thực tế của nhóm.

## Diễn tập trên bản sao dữ liệu nhóm — 07/10/2026

- Khôi phục `retrack-postgres-before-schema-cleanup.dump` vào database cục bộ riêng `Retrack_TV4_rehearsal`; database dùng chung `postgres` không bị migration lần này tác động.
- Chạy `db/cleanup_legacy_pascalcase_tables.sql`, sau đó cập nhật toàn bộ migration EF. Cả hai migration mới `ConsolidateSharedSchema` và `AddLegacyTransactionDetails` đều áp dụng thành công.
- Đối chiếu sau nâng cấp: `users` 7 hàng, `factories` 3, `factory_demands` 4, `platform_transactions` 3; dữ liệu legacy đã được chuyển, `platform_fee_invoices` không còn.
- Chạy lại `dotnet ef database update`: không có migration nào áp dụng lại. Lịch sử ghi nhận 15 migration.
- Giới hạn: đã xác nhận schema và dữ liệu bản sao sau khi chạy script hợp nhất; đây không phải browser E2E với backend thật hoặc kiểm chứng dữ liệu trên database dùng chung.

## Browser E2E với backend thật — 07/10/2026

- Chạy API local trỏ tới `Retrack_TV2_test`, không trỏ tới database dùng chung.
- Playwright dùng tài khoản Factory và Depot tạm, tạo lô đã có trạng thái vận chuyển `DELIVERED`, rồi thao tác trên giao diện Factory: nhận hàng, cân 105/5 kg, chốt KCS đạt và quyết toán.
- Kết quả: 2/2 E2E backend thật đạt (cả luồng nhu cầu tạo/tải lại/xóa và luồng nhận hàng–KCS–quyết toán/tải lại). Đối chiếu PostgreSQL: lô `COMPLETED`, khối lượng thực 100 kg, KCS đạt và một giao dịch phí `BATCH_ORDER` 6.000đ.
- Tài khoản, Depot, lô, KCS, giao dịch và nhu cầu fixture đã được xóa sau khi đối chiếu. Trạng thái giao vận được seed trước test; thao tác ứng dụng Driver chưa thuộc phạm vi TV4.
