# Triển khai RETRACK lên VPS (chỉ Backend + Database, có CI/CD tự động)

Phạm vi: **API .NET 8 + PostgreSQL 16** chạy bằng Docker Compose trên một VPS. **Không** triển khai frontend, mobile hay dịch vụ AI phân loại rác.
Mỗi lần merge vào `main`, GitHub Actions tự build image, đẩy lên GHCR, SSH vào VPS và chạy bản mới (sao lưu DB trước, lỗi thì tự quay về bản cũ).

## Cảnh báo bảo mật — làm ngay

Ảnh chụp trang quản lý VPS đang **hiện mật khẩu root dạng chữ rõ**. Coi mật khẩu đó là đã lộ:
1. Đổi mật khẩu root ngay (`passwd` trên VPS, hoặc nút đổi mật khẩu của nhà cung cấp).
2. Sau khi có SSH key (bước 3), tắt đăng nhập bằng mật khẩu: sửa `/etc/ssh/sshd_config` thành `PasswordAuthentication no` rồi `systemctl restart ssh`.
3. Không dán mật khẩu, `.env.production` hay private key vào chat, issue hay commit.

## Thành phần

| Thành phần | Chạy ở đâu | Ghi chú |
|---|---|---|
| `db` (postgres:16-alpine) | VPS, mạng nội bộ Docker | Không mở cổng ra Internet; dữ liệu trong volume `pg_data` |
| `api` (`ghcr.io/<owner>/retrack-api`) | VPS, cổng `8080` (`API_BIND:API_PORT`) | Ảnh upload trong volume `uploads` |
| `caddy` (tuỳ chọn, profile `tls`) | VPS, cổng 80/443 | Chỉ khi có tên miền; tự xin chứng chỉ HTTPS |

File trên VPS nằm ở `/opt/retrack`: `docker-compose.prod.yml`, `deploy-remote.sh`, `server-setup.sh`, `.env.production` (secret, chỉ ở VPS), `db/retrack-system.sql`, `backups/`.

## 1. Gỡ FITLY khỏi VPS (có sao lưu trước)

Làm trước khi cài RETRACK vì FITLY đang dùng RAM, ổ đĩa và cổng 80/443.

1. Chép script lên VPS và chạy (thay `IP` bằng IP VPS):
   ```bash
   scp deploy/teardown-fitly.sh root@IP:/root/
   ssh root@IP
   bash /root/teardown-fitly.sh            # sao lưu DB + .env → hỏi xác nhận → dừng và xoá container
   ```
   - Mặc định **giữ** volume dữ liệu và thư mục `/opt/fitly`. Chỉ khi chắc chắn mới thêm `--purge-volumes` (xoá hẳn dữ liệu) và `--prune-images` (giải phóng ổ đĩa).
   - Script dừng ngay, chưa xoá gì, nếu sao lưu lỗi hoặc file sao lưu rỗng.
2. **Tải file sao lưu về máy** (`/root/backups/fitly-final-*.dump`, `fitly-env-*.env`):
   ```bash
   scp "root@IP:/root/backups/fitly-*" .
   ```
3. **Tắt CD của FITLY** (repo `eco_fashion_be`: Actions → workflow Deploy → *Disable workflow*, hoặc xoá secret SSH). Nếu không, lần push tiếp theo sẽ dựng lại FITLY lên VPS.

## 2. Chuẩn bị VPS (một lần)

```bash
ssh root@IP "mkdir -p /opt/retrack-tmp"
scp -r deploy root@IP:/opt/retrack-tmp/
ssh root@IP "mkdir -p /opt/retrack && cp -a /opt/retrack-tmp/deploy/. /opt/retrack/ && rm -rf /opt/retrack-tmp && bash /opt/retrack/server-setup.sh --with-swap"
```
`server-setup.sh` cài Docker nếu thiếu, tạo `/opt/retrack/.env.production` với **mật khẩu Postgres và `JWT_KEY` ngẫu nhiên**, mở cổng ufw (nếu bật) và tạo swap 2 GB (VPS chỉ có 2 GB RAM). Chạy lại không ghi đè `.env.production`.

Sau đó sửa cấu hình:
```bash
nano /opt/retrack/.env.production
```
- `FRONTEND_URL`: địa chỉ frontend được gọi API (CORS), ví dụ `http://localhost:5173` nếu bạn chạy frontend ở máy mình.
- `SEED_DEMO=on|off`: nạp 26 tài khoản/kho/đơn demo ở **lần dựng DB đầu tiên** (README `db/` khuyến cáo tắt ở production thật; bản demo đồ án có thể để `on`). Đổi sau khi DB đã dựng không có tác dụng.
- Khoá Cloudinary / PayOS / Google nếu dùng; để trống thì tính năng đó tắt. Cổng firewall của nhà cung cấp VPS (panel GenCloud) cũng phải cho phép `8080` (hoặc 80/443 nếu dùng tên miền).

## 3. Cấp quyền cho CD (một lần)

1. Tạo SSH key **riêng cho CD** trên máy bạn và thêm public key vào VPS:
   ```bash
   ssh-keygen -t ed25519 -C retrack-cd -f retrack_cd        # để trống passphrase
   cat retrack_cd.pub | ssh root@IP "cat >> ~/.ssh/authorized_keys"
   ```
2. Trên GitHub repo (cần quyền admin): *Settings → Secrets and variables → Actions → New repository secret*:

   | Secret | Giá trị |
   |---|---|
   | `VPS_HOST` | IP VPS |
   | `VPS_USER` | `root` (hoặc user trong nhóm `docker`) |
   | `VPS_SSH_KEY` | **toàn bộ nội dung** file `retrack_cd` (private key) |
   | `VPS_PORT` | (tuỳ chọn) cổng SSH nếu khác 22 |

3. (Tuỳ chọn) *Settings → Environments → New environment* tên `production` để bắt duyệt tay trước khi deploy.
4. Xoá file `retrack_cd` và `retrack_cd.pub` khỏi máy sau khi đã dán vào GitHub; **không bao giờ commit chúng**.

## 4. Triển khai lần đầu

Workflow `Backend CD (VPS)` chạy khi push vào **`main`** có đụng `backend/`, `deploy/` hoặc `db/bootstrap/retrack-system.sql`; hoặc chạy tay ở tab Actions (*Run workflow*). File workflow phải nằm trên nhánh `main` thì mới có nút chạy tay, nên merge PR chứa các file này vào `main` trước.

Luồng: build → đẩy `ghcr.io/<owner>/retrack-api:<sha>` → chép file lên `/opt/retrack` → `deploy-remote.sh`:
sinh file khởi tạo DB → bật Postgres → (DB đã có dữ liệu) sao lưu `backups/pre-<giờ>.dump` → chạy API → chờ `/health/live` → lỗi thì quay về image cũ.

Kiểm tra:
```bash
curl http://IP:8080/health/live
ssh root@IP "cd /opt/retrack && docker compose --env-file .env.production -f docker-compose.prod.yml ps"
ssh root@IP "cd /opt/retrack && docker compose --env-file .env.production -f docker-compose.prod.yml logs --tail 100 api"
```

## 5. Vận hành

- **Nâng schema sau mỗi lần deploy có đổi `db/bootstrap/retrack-system.sql`.** Mã mới đọc cột/bảng mới; DB production chưa nâng thì các API liên quan trả 500. Ví dụ ngày 28/11/2026: bản deploy đọc cột lịch thu gom (`collection_date`, `preferred_end_datetime`, `requires_owner_review`, `dispatch_review_reason`) nhưng DB chưa có, nên `/api/seller/pickups` và `/api/depot/{id}/employee-dispatch/pickups` lỗi. Trên VPS, sau khi nhóm review SQL:
  ```bash
  cd /opt/retrack
  CHECK_ONLY=1 bash upgrade-schema.sh   # xem còn thiếu cột không
  bash upgrade-schema.sh                # sao lưu backups/pre-schema-<giờ>.dump, áp khối schema (không seed), kiểm tra lại
  docker compose --env-file .env.production -f docker-compose.prod.yml restart api
  ```
  Script chỉ áp phần từ đầu file tới `-- RETRACK_SCHEMA_END` trong một transaction; khối này dùng `IF NOT EXISTS` nên chạy lại vẫn an toàn. Đã thử nâng từ schema ngày 26/11 (có seed demo) lên bản hiện tại và chạy lại lần hai: không lỗi, giữ nguyên dữ liệu. CD chỉ chép script, không tự chạy.
- **Schema DB không tự đổi.** File `retrack-system.sql` chỉ chạy khi volume `pg_data` còn trống (lần đầu). Sửa schema sau đó do nhóm chạy tay (đúng quy ước `AGENTS.md`), ví dụ:
  `docker compose ... exec -T db psql -U retrack -d retrack < file.sql`. Muốn dựng lại từ đầu: `docker compose ... down -v` (**xoá dữ liệu**) rồi chạy lại CD.
- **Khôi phục DB** từ bản sao lưu: `docker compose ... exec -T db pg_restore -U retrack -d retrack --clean --if-exists < backups/pre-<giờ>.dump`.
- **Quay về bản cũ thủ công:** `IMAGE=ghcr.io/<owner>/retrack-api:<sha-cũ> bash /opt/retrack/deploy-remote.sh` (tag sha nằm ở tab Packages của repo).
- **Bật HTTPS (giống FITLY, không cần mua tên miền):** xem mục 6 bên dưới.
- **Ổ đĩa đầy** (log CD: `Postgres không sẵn sàng`, log DB: `PANIC ... No space left on device`): Postgres crash lặp vì không ghi được checkpoint. Dữ liệu vẫn nằm trong WAL, có chỗ trống là tự phục hồi. Từ bản này `deploy-remote.sh` tự dọn image API cũ/build cache/container dừng trước khi kéo image và dừng sớm nếu còn dưới `MIN_FREE_MB` (1500 MB); log container giới hạn 3 × 10 MB/service. Cứu tay trên VPS:
  ```bash
  df -h / /var/lib/docker; docker system df                       # xem cái gì chiếm chỗ
  cd /opt/retrack
  CUR=$(grep '^API_IMAGE=' .env.production | cut -d= -f2-)
  docker images "${CUR%:*}" --format '{{.Repository}}:{{.Tag}}' | grep -vxF "$CUR" | xargs -r docker rmi   # image API cũ, giữ bản đang chạy
  docker image prune -f; docker builder prune -f; docker container prune -f
  ls -1t backups/pre-*.dump | tail -n +4 | xargs -r rm -f          # chỉ giữ 3 bản sao lưu mới nhất (tải về máy trước nếu cần)
  find /var/lib/docker/containers -name '*-json.log' -size +50M -exec truncate -s 0 {} \;   # log container quá lớn
  journalctl --vacuum-size=200M; apt-get clean                       # log hệ thống, cache apt
  docker compose --env-file .env.production -f docker-compose.prod.yml logs --tail 20 db   # chờ "database system is ready"
  ```
  **Không** chạy `docker volume prune`, `docker system prune --volumes` hay `down -v`: sẽ xoá volume `pg_data` (mất dữ liệu). Sau đó chạy lại job CD (Re-run) hoặc `SKIP_PULL=1` như trên.
- **Dịch vụ AI phân loại rác** (`AI_Garbage_Classification`) chưa được triển khai; API dùng `AI_SERVICE_URL` (mặc định `http://127.0.0.1:5005`), nên tính năng AI của Seller sẽ lỗi cho tới khi bạn chạy dịch vụ đó và cập nhật biến này.
- **Quét hồ sơ Depot:** `VERIFICATION_SCANNER_MODE=Disabled` ở production nghĩa là API từ chối nộp hồ sơ mới (xem `backend/Retrack.API/.env.example`); cần ClamAV nếu muốn mở.

## 6. Chuyển sang HTTPS (sslip.io, như FITLY)

Let's Encrypt chỉ cấp chứng chỉ cho **tên miền**, không cấp cho IP trần. Nếu chưa có tên miền riêng, dùng sslip.io: `api.<IP-viết-bằng-dấu-chấm>.sslip.io` tự trỏ về đúng IP đó, ví dụ `api.160.191.242.187.sslip.io`. Caddy (profile `tls`) sẽ tự xin và gia hạn chứng chỉ; API chỉ nghe loopback, chỉ Caddy mở cổng 80/443.

1. Mở cổng **80 và 443** trong firewall của trang quản lý VPS (cổng 80 bắt buộc để Let's Encrypt xác minh).
2. Trên VPS, bật HTTPS (tự lấy IP công khai, đặt `DOMAIN` và `API_BIND=127.0.0.1` trong `.env.production`):
   ```bash
   bash /opt/retrack/server-setup.sh --enable-https
   ```
   (Hoặc sửa tay hai dòng đó trong `/opt/retrack/.env.production`; có tên miền riêng thì đặt `DOMAIN=ten-mien.com`.)
3. Áp dụng ngay, không cần chạy lại GitHub Actions (dùng image đang có sẵn trên VPS):
   ```bash
   cd /opt/retrack && IMAGE=$(grep '^API_IMAGE=' .env.production | cut -d= -f2-) SKIP_PULL=1 bash deploy-remote.sh
   ```
   Script sao lưu DB, bật Caddy cùng API. Chờ khoảng 1 phút lần đầu để xin chứng chỉ.
4. Kiểm tra: `curl https://api.<IP>.sslip.io/health/live` phải ra `{"status":"ok"}`.

Sau khi chuyển:
- **Webhook PayOS** (đăng ký tại my.payos.vn): `https://api.<IP>.sslip.io/api/payos/webhook`.
- Cổng `8080` từ ngoài **không còn vào được**; mọi nơi gọi API (frontend, `vercel.json`, mobile) phải dùng địa chỉ `https://api.<IP>.sslip.io`.
- Caddy cần cổng 80 mở mãi để gia hạn chứng chỉ (khoảng 60 ngày một lần).
- sslip.io là dịch vụ miễn phí dùng chung và có giới hạn số chứng chỉ; dùng lâu dài nên mua tên miền riêng.

## 7. Ký điện tử hợp đồng bằng DocuSign (sandbox)

Tính năng: hợp đồng hợp tác Nhà máy – Vựa (sau khi Nhà máy đề nghị, trạng thái `PENDING_DEPOT`) được hai bên ký ngay trong hệ thống qua DocuSign sandbox; ký đủ thì hợp đồng `ACTIVE`. Chỉ để minh hoạ đồ án, không có giá trị pháp lý. Nút "Ký chấp thuận" cũ của chủ vựa vẫn dùng được.

**Thứ tự bắt buộc: chạy SQL thêm cột TRƯỚC khi deploy bản mã mới** (mã mới đọc 5 cột mới của bảng `factory_depot_partnerships`, thiếu cột là mọi truy vấn bảng này lỗi). Trên VPS:
```bash
cd /opt/retrack && docker compose --env-file .env.production -f docker-compose.prod.yml exec -T db psql -U retrack -d retrack -v ON_ERROR_STOP=1 -c "ALTER TABLE factory_depot_partnerships ADD COLUMN IF NOT EXISTS esign_envelope_id VARCHAR(64), ADD COLUMN IF NOT EXISTS esign_status VARCHAR(30), ADD COLUMN IF NOT EXISTS esign_terms_hash VARCHAR(64), ADD COLUMN IF NOT EXISTS esign_sent_at TIMESTAMPTZ, ADD COLUMN IF NOT EXISTS esign_completed_at TIMESTAMPTZ;"
```
(DB dựng mới từ `db/bootstrap/retrack-system.sql` đã có sẵn các cột này.)

**Cấu hình** trong `/opt/retrack/.env.production` (không commit, không gửi vào chat):

| Biến | Lấy ở đâu |
|---|---|
| `DOCUSIGN_INTEGRATION_KEY` | Admin → Apps and Keys → app → Integration Key |
| `DOCUSIGN_USER_ID` | Apps and Keys → My Account Information → User ID |
| `DOCUSIGN_ACCOUNT_ID` | Apps and Keys → API Account ID |
| `DOCUSIGN_BASE_URI` | `https://demo.docusign.net` (sandbox) |
| `DOCUSIGN_OAUTH_HOST` | `account-d.docusign.com` (sandbox) |
| `DOCUSIGN_PRIVATE_KEY_BASE64` | File khóa RSA đã lưu, mã hoá base64 thành một dòng (xem dưới) |
| `DOCUSIGN_WEBHOOK_URL` | (tuỳ chọn) `https://api.<IP>.sslip.io/api/esign/docusign/webhook` |

Mã hoá khóa RSA thành một dòng (PowerShell trên máy bạn), rồi dán vào `.env.production` trên VPS:
```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("docusign_private.key"))
```
Ứng dụng DocuSign phải được cấp quyền (consent) một lần cho tài khoản (đường dẫn `https://account-d.docusign.com/oauth/auth?response_type=code&scope=signature%20impersonation&client_id=<Integration Key>&redirect_uri=<Redirect URI đã khai trong app>`).

`FRONTEND_URL` phải là địa chỉ frontend thật đang dùng (DocuSign đưa người ký quay về đó sau khi ký): nhà máy về `<FRONTEND_URL>/factory/`, chủ vựa về `<FRONTEND_URL>/depot/partners`.

Áp dụng cấu hình mới (tạo lại container API với biến mới):
```bash
cd /opt/retrack && IMAGE=$(grep '^API_IMAGE=' .env.production | cut -d= -f2-) SKIP_PULL=1 bash deploy-remote.sh
```
Webhook là tuỳ chọn: không có thì dùng nút "Cập nhật trạng thái" hoặc tự cập nhật khi người ký quay lại hệ thống.

## Giới hạn đã biết

- Chưa chạy thử trên VPS thật: các script đã kiểm tra cú pháp (`bash -n`) nhưng lần chạy đầu nên có người theo dõi.
- Image ở GHCR mặc định riêng tư; CD truyền token tạm để VPS kéo image. Muốn kéo thủ công thì cần `docker login ghcr.io` bằng token có quyền `read:packages`.
- VPS 1 vCPU / 2 GB RAM / 20 GB ổ đĩa chạy được API + Postgres nhưng không dư dả; giữ `backups/` gọn (script giữ 7 bản). Nếu DB lớn dần, giảm `KEEP_BACKUPS` hoặc tải bản sao lưu về máy rồi xoá bớt.
