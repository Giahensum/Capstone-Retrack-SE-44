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

- **Schema DB không tự đổi.** File `retrack-system.sql` chỉ chạy khi volume `pg_data` còn trống (lần đầu). Sửa schema sau đó do nhóm chạy tay (đúng quy ước `AGENTS.md`), ví dụ:
  `docker compose ... exec -T db psql -U retrack -d retrack < file.sql`. Muốn dựng lại từ đầu: `docker compose ... down -v` (**xoá dữ liệu**) rồi chạy lại CD.
- **Khôi phục DB** từ bản sao lưu: `docker compose ... exec -T db pg_restore -U retrack -d retrack --clean --if-exists < backups/pre-<giờ>.dump`.
- **Quay về bản cũ thủ công:** `IMAGE=ghcr.io/<owner>/retrack-api:<sha-cũ> bash /opt/retrack/deploy-remote.sh` (tag sha nằm ở tab Packages của repo).
- **Bật HTTPS:** trỏ tên miền về IP VPS, đặt `DOMAIN=ten-mien.com` và `API_BIND=127.0.0.1` trong `.env.production`, rồi chạy lại CD; Caddy sẽ tự xin chứng chỉ.
- **Dịch vụ AI phân loại rác** (`AI_Garbage_Classification`) chưa được triển khai; API dùng `AI_SERVICE_URL` (mặc định `http://127.0.0.1:5005`), nên tính năng AI của Seller sẽ lỗi cho tới khi bạn chạy dịch vụ đó và cập nhật biến này.
- **Quét hồ sơ Depot:** `VERIFICATION_SCANNER_MODE=Disabled` ở production nghĩa là API từ chối nộp hồ sơ mới (xem `backend/Retrack.API/.env.example`); cần ClamAV nếu muốn mở.

## Giới hạn đã biết

- Chưa chạy thử trên VPS thật: các script đã kiểm tra cú pháp (`bash -n`) nhưng lần chạy đầu nên có người theo dõi.
- Image ở GHCR mặc định riêng tư; CD truyền token tạm để VPS kéo image. Muốn kéo thủ công thì cần `docker login ghcr.io` bằng token có quyền `read:packages`.
- VPS 1 vCPU / 2 GB RAM / 20 GB ổ đĩa chạy được API + Postgres nhưng không dư dả; giữ `backups/` gọn (script giữ 7 bản).
