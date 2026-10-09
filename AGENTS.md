# RETRACK — quy tắc cộng tác

## Khảo sát trước khi sửa

- Đọc yêu cầu, `git status`, branch/remote và diff trước khi làm việc. Bảo toàn thay đổi của người khác; không tự reset, clean, stash, force-push hoặc ghi đè file.
- Đọc `.ai-context` và hướng dẫn theo thư mục nếu có. Các tài liệu này có thể chỉ tồn tại local; không force-add file đang ignore.
- Đối chiếu code thực chạy với tài liệu: nhiều controller/service dưới thư mục role hiện còn scaffold. Không coi TODO hoặc dữ liệu mock là chức năng đã hoàn thành.

## Phân công và phạm vi

| Thành viên | Phạm vi chính |
|---|---|
| TV1 | Auth, Seller |
| Ngô Sỹ Giá (TV2) | Chủ kho vựa (Depot Owner) |
| TV3 | Depot Employee, Driver |
| TV4 | Factory |
| TV5 | Admin, hạ tầng |

- Frontend theo `frontend/src/features/<role>`. Depot đang dùng các trang trực tiếp trong `features/depot`, không phải các trang scaffold trong `features/depot/pages`.
- Backend chạy ở `backend/Retrack.API`. Auth/Pickup hiện còn nằm ở `Services/AuthService.cs`, `Services/PickupService.cs`, controller dùng chung trong `Controllers/AppControllers.cs`.
- Thay đổi cần thiết ngoài role phải tối thiểu, giữ tương thích và ghi rõ lý do/phụ thuộc trong PR. Các file dùng chung như `Program.cs`, `AppDbContext`, migration, router, auth store và Axios cần review cẩn thận.

## Git và PR

- Fetch và xác định mốc `origin/develop` trước khi tạo nhánh task; không code trực tiếp trên `main/develop`. Theo quy ước nhóm `feature/TVx-ten-chuc-nang`, hoặc tên người dùng chỉ định.
- Nếu working tree đang có việc khác, bảo toàn nó và dùng worktree khi phù hợp. Không tự commit thay đổi của người khác.
- Không tự push, merge PR, deploy hoặc áp dụng migration lên database dùng chung nếu yêu cầu chưa cho phép.
- Trước bàn giao: xem diff và untracked, kiểm tra secret, chạy checks phù hợp; nêu rõ Passed / Failed / Not Run và giới hạn kiểm thử. Commit có scope, ví dụ `feat(depot): ...`; stage các file cụ thể.

## Backend và database

- Controller xử lý HTTP/claims, gọi service; service giữ validation, quyền sở hữu, trạng thái, transaction. DTO trả dữ liệu tối thiểu, không serialize entity graph.
- Dùng contract đang hoạt động trong `DTOs/AppDtos.cs` (`ApiResponse`, `PagedResult`), không thêm bản sao từ `Helpers`/scaffold. Depot DTO nằm ở `DTOs/Depot`.
- JWT role chưa đủ: phải scope theo người dùng, kho và liên kết staff. Một owner có thể có nhiều kho. Kiểm tra tài khoản/membership đang hoạt động; không tin ownerId/role từ request body.
- Phân trang có giới hạn và thứ tự ổn định; tổng hợp/filter trong database. Không trả toàn bộ dữ liệu rồi lọc trên frontend.
- Thanh toán phải nguyên tử, kiểm tra state và xử lý retry. Một nguồn giao dịch chỉ có một platform transaction. Khóa dòng PostgreSQL khi cần chống race.
- Tồn kho chỉ nhận đơn `DONE`; phân biệt hàng giữ cho lô, hàng xuất và tồn khả dụng. Mọi service phân bổ/hủy lô cùng kho phải dùng cùng khóa dòng depot. Factory/Driver cần tuân thủ giao thức này khi thay đổi phân bổ.
- Giữ nguyên mức phí đã chốt trên đơn cũ. Không tự tạo công thức lương/lợi nhuận hoặc coi QC bị từ chối là hàng đã trả về kho.
- Hiện tại ứng dụng không tự chạy EF migration; schema triển khai cho máy mới nằm trong `db/bootstrap/01-retrack-system.sql`, seed chung ở `db/bootstrap/02-seed-data.sql`. Giữ cơ chế này tới khi nhóm chốt việc chuyển DB-first. Review SQL trước khi áp dụng; không sửa migration đã áp dụng/chia sẻ, không tạo schema song song hoặc dùng EnsureCreated trên DB có migration. Không reset/drop DB để sửa lỗi kiểm thử.
- Model map thực tế có nhiều trạng thái string và entity ở `Models/OtherModels.cs`; enum/model scaffold chưa chắc là contract hiện hành. Ghi rõ state mapping khi liên kết role.

## Frontend

- Viết tài liệu, hướng dẫn bàn giao, chú thích do mình bổ sung và nội dung giao diện bằng tiếng Việt để người phụ trách dễ đọc. Giữ nguyên tên API, biến, lớp, trạng thái, đường dẫn, tên nhánh và tên database khi chúng là định danh kỹ thuật.

- Tái sử dụng `src/lib/axios`, Zustand auth store, React Query và route guard. Không tạo token store/API client song song; không đưa JWT/secret vào URL/log.
- Duy trì theme Depot, responsive, nhãn biểu mẫu, trạng thái loading/error/empty/success, khóa nút khi mutation chạy và invalidate query đúng scope.
- Thay mock bằng API thật, giữ validation server làm nguồn quyết định. Không hiển thị số liệu giả để che API thiếu; ghi rõ phụ thuộc trong tài liệu.

## Cấu hình và kiểm thử

- Secret chỉ dùng biến môi trường hoặc `backend/Retrack.API/.env` local đã ignore; mẫu dùng `.env.example`. Không ghi mật khẩu DB, token hay key vào code, frontend, log hoặc tài liệu.
- Giữ nguyên tài khoản/mật khẩu PostgreSQL local của Ngô Sỹ Giá khi đồng bộ nhánh. `appsettings.json` là cấu hình riêng, không theo dõi bằng Git; dùng `appsettings.example.json` làm bản mẫu chia sẻ. Không thay file local bằng cấu hình từ develop.
- `.env` chỉ nạp ở Development; biến môi trường có sẵn được ưu tiên. `Database__Initialize=true` chỉ seed dữ liệu demo khi schema đã tồn tại; ứng dụng không tự migrate. Không bật seed tài khoản demo ở production.
- Backend: `dotnet restore backend/Retrack.sln`, `dotnet build backend/Retrack.sln --no-restore`.
- PostgreSQL tests: đặt cả `RETRACK_TEST_CONNECTION` và `RETRACK_TEST_POSTGRES` tới đúng `Retrack_TV2_test`, rồi `dotnet test backend/Retrack.sln --no-restore`. Tests chỉ dọn dữ liệu fixture của mình; xác minh tên database trước khi chạy.
- Frontend: trong `frontend`, chạy `npm ci`, `npm run lint`, `npm run build`. Lint dùng Oxlint đã khai báo; các file scaffold rỗng hiện có cảnh báo.
- Luồng tài chính/tồn kho cần thử validation, sai role/owner, retry, race, rollback, persistence. Kiểm thử trình duyệt cần thao tác thật và tải lại; build hoặc HTTP 200 chưa đủ để kết luận E2E.
- Hồ sơ kiểm chứng và các phụ thuộc hiện tại: `.ai-context/depot-owner/tien-do/depot-integration.md`.
