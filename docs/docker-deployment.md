# Đóng gói và triển khai Docker

Bộ Docker nằm trên nhánh `feature/TV5-docker-deployment`. Các lệnh dưới chạy từ thư mục gốc repository, dùng Docker Compose v2 và Linux containers.

Mốc code đóng gói là `aa7469a`, xuất phát từ `feature/TV1-ai-model-dataset`. Nhánh này chưa chứa thay đổi notification Seller và bộ unit test thông báo đã thực hiện ở các lượt trước. Cần đồng bộ những thay đổi đó vào nhánh phát hành nếu muốn deploy cả chức năng notification mới; lần tạo Docker này không tự merge thay đổi nghiệp vụ.

## Thành phần

| Dịch vụ | Đóng gói | Dữ liệu bền vững |
|---|---|---|
| `web` | Node build React/Vite, Nginx phục vụ SPA và proxy `/api` | Bundle nằm trong image |
| `api` | .NET SDK build, ASP.NET Core 8 runtime, user `app` | Volume `uploads` cho tệp Factory |
| `db` | PostgreSQL 17, không mở cổng DB ra host | Volume `postgres_data` |
| `ai` | Profile tùy chọn, Python 3.11 + PyTorch CPU | Model mount chỉ đọc, volume `ai_cache` cho EasyOCR |
| `gateway` | Caddy, chỉ bật với `compose.https.yaml` | Certificate trong `caddy_data` |

Mobile/Expo được build và phân phối riêng; không chạy giao diện mobile trong container web.

## Chuẩn bị cấu hình

Tạo `.env` ở gốc từ [.env.example](../.env.example) nếu chưa có, điền `POSTGRES_PASSWORD` và `JWT_KEY` riêng. Có thể tự sinh mỗi giá trị bằng `openssl rand -hex 32`. Dùng chuỗi hex để tránh ký tự đặc biệt trong connection string. Không đưa file `.env` vào Git hay gửi nội dung `docker compose config` đầy đủ vì có thể chứa secret; dùng `config --quiet` để kiểm tra.

Giữ `POSTGRES_DB`/`POSTGRES_USER` là định danh PostgreSQL thông thường, không chứa dấu chấm phẩy/dấu nháy. Không thay mật khẩu hay cấu hình PostgreSQL local của thành viên nhóm. Container dùng database riêng; không copy `backend/Retrack.API/appsettings.json` hoặc `.env` local vào image.

- `PUBLIC_URL`: URL người dùng truy cập, ví dụ localhost khi thử máy cá nhân; đổi sang URL HTTPS của domain khi deploy.
- `WEB_BIND_IP=127.0.0.1`, `WEB_PORT=8080`: mặc định chỉ mở web trên máy host. Dùng gateway HTTPS khi đưa lên Internet.
- Điền Cloudinary, SMTP, PayOS theo tính năng cần chạy. Chưa có cấu hình thì các dịch vụ bên ngoài tương ứng chưa hoạt động.
- `VITE_*` là cấu hình công khai trong bundle: Google client ID, Maps/Goong key phải giới hạn domain tại nhà cung cấp. Thay giá trị này cần build lại image web. Không đưa secret backend vào build args.
- `IMAGE_PREFIX` và `IMAGE_TAG` xác định tên image; khi phát hành dùng tag phiên bản riêng thay cho `local`.

Kiểm tra cấu hình mà không in secret:

```sh
docker compose config --quiet
```

## Chạy lần đầu với database container mới

Build và chạy database trước:

```sh
docker compose build api web
docker compose up -d --wait db
```

Review `db/bootstrap/retrack-system.sql`, sau đó chủ động khởi tạo schema:

```sh
docker compose exec -T db sh /opt/retrack/init-db.sh
```

Script dùng trực tiếp phần schema trước `RETRACK_SCHEMA_END` trong file gốc, tắt seed demo và commit transaction. Script từ chối chạy nếu database đã có bảng; không reset/drop, không dùng EnsureCreated, không tạo schema hay migration song song. Không mount bootstrap vào `/docker-entrypoint-initdb.d`, không tự áp SQL mỗi lần container chạy.

Với DB cũ có dữ liệu, không dùng script khởi tạo. Sao lưu và review SQL nâng cấp theo [db/README.md](../db/README.md). Thay đổi mật khẩu trong `.env` không tự đổi mật khẩu của role đã tồn tại trong volume PostgreSQL.

Khởi động ứng dụng sau khi schema đã sẵn sàng:

```sh
docker compose up -d --no-build --wait api web
docker compose ps
```

Mở `http://localhost:8080`. Database mới không có tài khoản demo. Tạo tài khoản theo luồng đăng ký của ứng dụng; không bật seed demo ở production. Quyền Admin và các dữ liệu nghiệp vụ ban đầu cần quy trình cấp quyền của nhóm.

## Bật AI

Chuẩn bị hai file `model.pth` và `yolov8n.pt` từ model đã huấn luyện/kiểm chứng. `AI_MODEL_DIR` mặc định là thư mục AI trong repository; trên server có thể trỏ tới đường dẫn tuyệt đối chứa hai file này.

```sh
docker compose --profile ai build ai
docker compose --profile ai up -d --wait ai
docker compose --profile ai ps
```

Weights không nằm trong image hoặc build context; cần chuyển riêng hai file model tới server. Container AI dừng nếu classifier weights thiếu hoặc không nạp được, tránh phục vụ dự đoán từ model chưa huấn luyện. Chỉ mount model từ nguồn do nhóm kiểm soát vì code hiện tải checkpoint bằng PyTorch pickle.

EasyOCR tải model ngôn ngữ trong lần khởi động đầu, cần Internet và có thể mất vài phút; cache lưu trong volume. AI dùng CPU, chưa cấu hình GPU/CUDA. Không cài requirements train hiện có vì chứa wheel CUDA; Docker dùng `requirements.docker.txt` dành cho inference. Chưa bật profile thì chức năng AI cần `AI_SERVICE_URL` trỏ tới một AI service khác; nếu không có, API sẽ báo dịch vụ AI không khả dụng.

## Deploy HTTPS với domain

Trỏ DNS domain về IP server, mở cổng 80/443 tại firewall. Trong `.env`, điền `DOMAIN`, `TLS_EMAIL` và đổi `PUBLIC_URL=https://<domain>`. Giữ web bind localhost; chỉ gateway công bố cổng 80/443.

Sau khi thực hiện bước schema trên server mới:

```sh
docker compose -f compose.yaml -f compose.https.yaml config --quiet
docker compose -f compose.yaml -f compose.https.yaml up -d --build --wait
```

Nếu dùng AI, thêm `--profile ai` trước `up`. Caddy xử lý TLS và chuyển hướng HTTP sang HTTPS. API chỉ nhận HTTP trong mạng Docker; Compose tắt redirect HTTPS nội bộ bằng `HttpsRedirection__Enabled=false`, tránh redirect tại hop không có chứng chỉ. Ngoài Compose, hành vi redirect production của API vẫn giữ mặc định.

Không public trực tiếp cổng API, DB hoặc AI. Nếu đã có Nginx/Traefik/load balancer ngoài, có thể chỉ dùng compose chính và proxy HTTPS về web tại localhost:8080; không bật thêm gateway chiếm cổng. X-forwarded headers hiện được gửi bởi Nginx nhưng chưa có cấu hình tin cậy proxy trong API; không dựa vào chúng cho audit IP client hoặc URL tự sinh.

## Chuyển image sang server

Có thể build trên server bằng source. Nếu dùng registry, cấu hình `IMAGE_PREFIX=<registry>/<namespace>` và tag phiên bản, tự đăng nhập registry và push khi sẵn sàng phát hành:

```sh
docker compose build api web
docker compose push api web
```

AI dùng `docker compose --profile ai build ai` và `docker compose --profile ai push ai`. Trên server có cùng Compose, `.env`, thư mục `deploy`, SQL bootstrap và model riêng, dùng `docker compose pull` rồi `docker compose up -d --no-build --wait`. Domain HTTPS dùng thêm `-f compose.yaml -f compose.https.yaml` vào mỗi lệnh; AI dùng thêm `--profile ai`. Không cần push PostgreSQL/Caddy/Nginx base image.

Không có lệnh push hoặc deploy máy chủ nào được agent thực thi trong lần tạo bộ Docker này.

## Vận hành và sao lưu

```sh
docker compose ps
docker compose logs --tail=100 api web
docker compose stop
docker compose start
```

`docker compose down` dừng và gỡ container/network, giữ volume. Không thêm `-v` với dữ liệu cần giữ. Khi cập nhật ứng dụng, giữ tên project Compose và volume để không chuyển sang database rỗng.

Sao lưu database vào file trong container rồi copy ra host, tránh redirect nhị phân qua PowerShell cũ:

```sh
docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f /tmp/retrack.dump'
docker compose cp db:/tmp/retrack.dump ../retrack.dump
docker compose exec -T api tar -czf /tmp/uploads.tar.gz -C /app uploads
docker compose cp api:/tmp/uploads.tar.gz ../uploads.tar.gz
```

Backup chứa dữ liệu riêng tư; lưu ngoài repository, kiểm tra khôi phục trước khi nâng cấp. Sao lưu cả uploads cùng DB; credentials Cloudinary và tệp lưu trên Cloudinary được quản lý riêng.

Healthcheck: `/health/web` kiểm tra Nginx, `/health/live` kiểm tra tiến trình API, `/health/ready` qua web kiểm tra kết nối DB và khả năng đọc bảng users (503 khi chưa sẵn sàng). Readiness không xác minh mọi bảng/schema hay mọi tích hợp bên ngoài. Healthcheck trong image API dùng liveness; cần theo dõi readiness riêng trước khi đưa traffic vào server.

## Hồ sơ kiểm chứng

| Kiểm tra | Kết quả |
|---|---|
| Compose chính và bản ghép HTTPS `config --quiet` | Passed |
| Docker build web, API và AI CPU trên Linux amd64 | Passed |
| Build solution .NET ở máy host | Passed, còn cảnh báo async có sẵn trong AppControllers |
| PostgreSQL mới: schema-only, không seed tài khoản | Passed; script từ chối khởi tạo lần hai |
| Chạy db/api/web/ai trong project riêng `retrack-docker-check` | Passed, healthcheck healthy |
| SPA fallback `/seller/notifications`, readiness và chặn API riêng tư khi chưa đăng nhập | Passed |
| Nginx → API → PostgreSQL: đăng ký Seller, tạo lô retry, tạo pickup và đọc notification thu gom | Passed |
| API chạy user non-root và không chứa appsettings/.env local | Passed |
| Recreate API: volume uploads, dữ liệu DB và proxy Nginx vẫn hoạt động | Passed |
| Caddy validate cấu hình HTTPS | Passed; chưa xin chứng chỉ domain thật |
| AI nạp model hiện tại, EasyOCR tải cache và inference ảnh tổng hợp | Passed HTTP 200; không kết luận độ chính xác model |
| Nginx → API → AI qua DNS nội bộ với contract image_base64 | Passed |
| Readiness khi DB dừng/phục hồi; AI thiếu model phải dừng | Passed; readiness 503 khi DB dừng, 200 khi phục hồi; liveness vẫn 200 |

Không áp SQL hoặc sửa cấu hình vào PostgreSQL local của thành viên nhóm. Không push image, không deploy máy chủ và chưa kiểm thử chứng chỉ/DNS thật, PayOS, Cloudinary, SMTP, Google hoặc GPU. Docker image được kiểm chứng trên Linux amd64; nền tảng ARM64 cần build và kiểm chứng riêng, nhất là dependency AI.

Tài liệu tham chiếu: [Docker Compose và biến môi trường](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/), [Nginx official image](https://hub.docker.com/_/nginx), [PyTorch CPU wheels](https://pytorch.org/get-started/previous-versions/).
