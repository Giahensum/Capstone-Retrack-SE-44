# Triển khai frontend RETRACK lên Vercel

## Phạm vi và cơ chế kết nối

- Frontend React/Vite nằm trong `frontend`; Vercel chỉ build thư mục này.
- Giữ Axios dùng `baseURL: '/api'`, JWT interceptor, auth store và API contract hiện tại.
- Khi chạy local, `vite.config.js` proxy `/api` tới `API_PROXY_TARGET` hoặc `http://localhost:5000`.
- Khi triển khai, `frontend/vercel.json` proxy `/api/:path*` tới backend Azure và trả `index.html` cho đường dẫn React Router như `/login`, `/depot/dashboard`.
- Rule API đứng trước rule SPA, giữ nguyên tiền tố `/api` trên backend. Không thay bằng redirect.
- Cấu hình này dùng chung cho các role vì cùng sử dụng Axios và router. Không thay nghiệp vụ, backend hoặc database.

Luồng đăng nhập:

```text
Trình duyệt -> https://<frontend>.vercel.app/api/auth/login
Vercel      -> http://retrack-api-se44.japaneast.cloudapp.azure.com:8080/api/auth/login
```

Backend hiện được cung cấp bằng HTTP. Trình duyệt gọi HTTPS tới Vercel nên tránh gọi API HTTP trực tiếp gây Mixed Content, nhưng đoạn Vercel tới Azure vẫn chưa mã hóa. Cấu hình này phục vụ demo; trước khi dùng tài khoản/dữ liệu thật, TV5 cần cấu hình HTTPS có chứng chỉ hợp lệ cho backend rồi đổi `destination` trong `vercel.json` sang địa chỉ HTTPS thực tế. Không chỉ đổi chữ `http` thành `https` nếu máy chủ chưa phục vụ HTTPS.

## 1. Quy trình Git và chọn nhánh

1. Fetch `origin`, tạo nhánh task từ `origin/develop`, bảo toàn công việc local bằng worktree khi cần.
2. Commit riêng cấu hình và tài liệu; push nhánh task, tạo PR có base `develop` để nhóm review.
3. Có thể deploy thử nhánh feature trước khi merge. Bản phát hành chính đi qua PR `feature -> develop`, kiểm tra tích hợp, rồi PR `develop -> main`.
4. Trên Vercel, đặt Production Branch là `main` và kiểm tra deployment thực tế dùng đúng branch/commit. Đây là lựa chọn của nhóm cho phiên bản `v1.0.0`.

Việc tạo project Vercel không tự merge các nhánh. Sau khi kết nối GitHub và bật tự triển khai, mỗi commit mới trên `main` có thể kích hoạt một lần build/deploy tự động. Chỉ cần tạo project và cấu hình biến một lần; không nhập lại biến mỗi khi có code mới. Thay đổi chỉ ở `develop` chưa cập nhật bản chính cho tới khi được merge vào `main`. Git integration còn phụ thuộc quyền tài khoản, tác giả commit và giới hạn gói Vercel.

## 2. Tạo project Vercel

1. Đăng nhập Vercel bằng GitHub. Dùng Hobby cho đồ án/demo cá nhân phi thương mại trong hạn mức gói.
2. Chọn **Add New > Project**, import `Giahensum/Capstone-Retrack-SE-44`.
3. Nếu thiếu repository, cấp quyền GitHub integration cho đúng repository. Hobby có giới hạn với repository private thuộc organization; kiểm tra điều kiện tài khoản trước khi chọn phương án nhóm.
4. Nhập cấu hình sau:

| Thiết lập | Giá trị |
| --- | --- |
| Project Name | Tên còn trống, ví dụ `retrack-fe` |
| Framework Preset | `Vite` |
| Root Directory | `frontend` |
| Install Command | `npm ci` |
| Build Command | `npm run build` |
| Output Directory | `dist` |
| Node.js Version | `24.x` |

`vercel.json` đã khai báo framework và các lệnh build. Root Directory vẫn phải chọn trên dashboard; khi đã chọn `frontend`, không nhập output là `frontend/dist`. Vercel build trực tiếp, không dùng Dockerfile/nginx của frontend.

## 3. Biến môi trường frontend

Tại màn hình tạo project hoặc **Project > Settings > Environment Variables**, thêm:

| Key | Value | Chức năng |
| --- | --- | --- |
| `VITE_GOOGLE_CLIENT_ID` | Google OAuth Client ID của nhóm | Đăng nhập Google |
| `VITE_GOONG_API_KEY` | Khóa Goong dành cho trình duyệt của nhóm | Gợi ý địa chỉ |

Chọn Production và Preview nếu muốn dùng ở cả bản chính và bản xem thử. Ô Key chỉ chứa tên biến; ô Value chỉ chứa giá trị, không nhập cả chuỗi `KEY=value`.

**Không cần `VITE_API_URL`**: Axios hiện không đọc biến này. Địa chỉ backend nằm trong `rewrites` của `frontend/vercel.json`. `API_PROXY_TARGET` chỉ phục vụ Vite dev local, không cấu hình proxy Vercel.

Vite nhúng các biến `VITE_*` vào JavaScript trình duyệt lúc build, nên đây không phải kho secret. Không nhập connection string, mật khẩu database, JWT signing key, Google Client Secret, PayOS key hoặc Cloudinary API secret. Không commit `.env.local`.

Sau khi thêm/sửa biến, lưu rồi tạo deployment mới hoặc **Deployments > chọn deployment > ... > Redeploy**. Bản đang chạy không tự nhận biến vừa đổi.

`Redeploy` là tạo bản triển khai mới, có thể được kích hoạt tự động bởi Git hoặc chủ động trên dashboard. Đây là hoạt động bình thường vì Vite nhúng cấu hình lúc build. Không phải lỗi của project. Nếu đổi backend với kiến trúc hiện tại, sửa `destination` của rule API trong `vercel.json`, đưa qua PR vào `main`; không thêm `VITE_API_URL` rồi chờ Axios tự đổi.

Google OAuth: thêm origin frontend thực tế vào Authorized JavaScript origins của OAuth client. Goong: cập nhật domain được phép nếu khóa có giới hạn domain. Preview có thể có origin khác; không mặc định mọi Preview đã được cho phép.

## 4. Phụ thuộc backend Azure — TV5 phối hợp chủ role

Trước khi kết luận frontend gọi được API, cần kiểm tra từ bên ngoài Azure:

```powershell
Test-NetConnection retrack-api-se44.japaneast.cloudapp.azure.com -Port 8080
curl.exe -i --max-time 20 http://retrack-api-se44.japaneast.cloudapp.azure.com:8080/api/auth/login
```

GET vào endpoint chỉ nhận POST có thể trả 405; đây chỉ là kiểm tra HTTP, không phải kiểm thử đăng nhập. Không truyền mật khẩu/token vào lệnh hoặc log chia sẻ.

- Kiểm tra VM/container, port publish, địa chỉ lắng nghe, Azure NSG và firewall khi không kết nối được.
- `Program.cs` gọi `UseHttpsRedirection()` ngoài Development. Nếu HTTP redirect tới HTTPS chưa hoạt động thì phải sửa cấu hình HTTPS/reverse proxy ở hạ tầng; không tắt bảo vệ hoặc chuyển Production thành Development để né lỗi.
- Sau khi có URL frontend, đặt `Frontend__Url=https://<domain-frontend-thuc-te>` ở môi trường backend và khởi động lại dịch vụ theo quy trình triển khai. CORS hiện đọc `Frontend:Url`; code backend cũng có thể sử dụng URL này cho liên kết/chuyển hướng.
- Với proxy cùng origin, trình duyệt gọi domain Vercel. Nếu chuyển sang gọi trực tiếp backend HTTPS trong tương lai thì phải đồng thời sửa Axios đọc base URL và cấu hình CORS cho origin frontend được phép.
- Nếu cấu hình forwarded headers ở backend, chỉ tin các proxy đã xác định; không tin mọi IP do phía client tự gửi.
- Không thay PostgreSQL local, không áp migration/seed hoặc sửa database dùng chung để deploy frontend.

## 5. Deploy và kiểm tra thực tế

1. Chọn **Deploy**, đọc build log, đợi trạng thái Ready.
2. Kiểm tra branch/commit của deployment có chứa `frontend/vercel.json`.
3. Mở domain được cấp, vào `/login`, nhấn F5 và mở trực tiếp URL này ở tab mới.
4. Đăng nhập bằng tài khoản thử nghiệm hợp lệ; kiểm tra F12 > Network: request là `https://<frontend>/api/...`, phản hồi API là JSON phù hợp.
5. Mỗi chủ role thử quyền truy cập, tải danh sách/chi tiết, thao tác được phép rồi tải lại trang để kiểm tra persistence. Sai role và thiếu token phải bị từ chối phù hợp.
6. Thử upload với kích thước hợp lệ theo tính năng, Google login và gợi ý địa chỉ nếu đã cấu hình dịch vụ.
7. Nhờ thành viên khác truy cập từ máy/mạng khác. Không dùng tài khoản thật qua đoạn kết nối backend HTTP.

| Hiện tượng | Cần kiểm tra |
| --- | --- |
| Tải lại `/login` bị 404 | Root Directory, file config và commit đã deploy |
| API trả HTML | Rule API thiếu/sai hoặc bị rule SPA bắt trước |
| API 502/504 | Backend Azure, mạng, timeout, HTTPS redirect |
| Mixed Content | Frontend đang gọi trực tiếp URL HTTP ở đâu đó |
| Biến mới chưa có hiệu lực | Environment đã chọn và deployment mới |
| Google login lỗi origin | Client ID và danh sách origin của OAuth client |

## 6. Kiểm chứng trước bàn giao

Trong `frontend` chạy `npm ci`, `npm run lint`, `npm run build`. Phân biệt kết quả build/lint với E2E. Chỉ đánh dấu E2E Passed sau khi thao tác thật trên deployment và tải lại để kiểm tra dữ liệu.

Kết quả kiểm tra local ngày 15/11/2026 trên nhánh cấu hình, nền `origin/develop` tại `c02d4aa`:

| Kiểm tra | Kết quả |
| --- | --- |
| `npm ci` theo lockfile | Passed |
| `npm run lint` | Passed; 7 cảnh báo có sẵn về file rỗng/import chưa dùng |
| `npm run build` | Passed; còn cảnh báo chunk lớn hơn 500 kB |
| Đọc JSON, thứ tự API/SPA, đích `/api/auth/login` | Passed bằng kiểm tra Node |
| HTTP tới backend Azure cổng 8080 | Failed từ môi trường kiểm tra: timeout sau 20 giây; chưa xác định nguyên nhân |
| Deploy Vercel và E2E trình duyệt | Not Run; chưa có deployment trong phạm vi lượt sửa này |

Build thành công không chứng minh backend đang truy cập được hoặc mọi role đã hoàn thiện. Không có thay đổi backend/database trong nhánh này.

## Tài liệu chính thức

- [Vite trên Vercel và SPA fallback](https://vercel.com/docs/frameworks/frontend/vite)
- [External rewrites](https://vercel.com/docs/routing/rewrites)
- [Biến môi trường Vercel](https://vercel.com/docs/environment-variables)
- [Biến môi trường Vite](https://vite.dev/guide/env-and-mode)
- [Triển khai từ Git và giới hạn Hobby](https://vercel.com/docs/git)
- [Gói Hobby](https://vercel.com/docs/plans/hobby)
