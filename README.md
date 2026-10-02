# 🔄 RETRACK — Recycling Tracking Platform

> Nền tảng số hóa chuỗi cung ứng phế liệu, kết nối Người bán → Kho vựa → Nhà máy tái chế.

## 🏗️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 8.0, Entity Framework Core |
| Frontend | Vite + React |
| Database | PostgreSQL |
| Auth | JWT + Google OAuth |
| Storage | Cloudinary (Image CDN) |
| Maps | Mapbox GL JS |

## 📁 Cấu trúc thư mục

```
Capstone/
├── backend/                        ← ASP.NET Core API (Repository-Service Pattern)
│   └── Retrack.API/
│       ├── Controllers/            ← 🎮 API Endpoints (chia theo Role)
│       │   ├── Auth/               ← Login, Register, Google OAuth
│       │   ├── Seller/             ← TV1: Pickup request, confirm
│       │   ├── Depot/              ← Ngô Sỹ Giá: tổng quan, tồn kho, lô hàng, thanh toán, nhân sự
│       │   ├── Employee/           ← TV3: Accept order, check-in, sort & weigh
│       │   ├── Driver/             ← TV3: Accept job, check-in/out
│       │   ├── Factory/            ← TV4: Marketplace, QC, settlement, partner
│       │   ├── Admin/              ← TV5: User management, fee config
│       │   └── Shared/             ← Notification, image upload
│       ├── Services/               ← 🧠 Business Logic (chia theo Role)
│       │   ├── Interfaces/         ← Interface definitions
│       │   ├── Auth/               ← TV1
│       │   ├── Seller/             ← TV1
│       │   ├── Depot/              ← Ngô Sỹ Giá (TV2)
│       │   ├── Employee/           ← TV3
│       │   ├── Driver/             ← TV3
│       │   ├── Factory/            ← TV4
│       │   ├── Admin/              ← TV5
│       │   └── Shared/             ← Cloudinary, Notification
│       ├── Repositories/           ← 💾 Data Access (EF Core)
│       │   └── Interfaces/
│       ├── Models/                 ← 📋 Entity Classes
│       │   └── Enums/
│       ├── DTOs/                   ← 📨 Request/Response objects (chia theo Role)
│       ├── Data/                   ← 🗄️ DbContext + Seeder
│       ├── Middleware/             ← 🛡️ Exception handling
│       ├── Helpers/                ← 🔧 ApiResponse, Pagination
│       └── Program.cs             ← ⚡ Entry point + DI configuration
│
├── frontend/                       ← Vite + React (Feature-Based)
│   └── src/
│       ├── app/                    ← Auth guard, routes
│       ├── components/             ← Shared UI: Button, Modal, Table, Map...
│       │   ├── ui/                 ← Atomic components
│       │   ├── layout/             ← Header, Sidebar, DashboardLayout
│       │   ├── maps/               ← MapView (Mapbox)
│       │   └── charts/             ← BarChart, DonutChart
│       ├── features/               ← ⭐ Mỗi người code trong folder role mình
│       │   ├── auth/               ← TV1: Login, Register
│       │   ├── seller/             ← TV1: Dashboard, CreateRequest
│       │   ├── depot/              ← Ngô Sỹ Giá: tổng quan, tồn kho, nhân sự...
│       │   ├── employee/           ← TV3: PickupPool, CheckIn, Weigh
│       │   ├── driver/             ← TV3: TransportPool, CheckIn/Out
│       │   ├── factory/            ← TV4: Marketplace, QC, Settlement
│       │   └── admin/              ← TV5: UserManagement, FeeConfig
│       ├── lib/                    ← Axios instance, Mapbox config
│       ├── hooks/                  ← Custom hooks
│       └── utils/                  ← Format, validators, constants
│
├── docs/                           ← Reports (RP1-RP5)
├── sql/                            ← Database scripts
├── tests/                          ← Unit + Integration tests
└── README.md
```

## 🔄 Luồng code 1 feature

```
1️⃣  DTOs/       → Tạo Request/Response class
2️⃣  Services/   → Viết business logic
3️⃣  Controllers/→ Tạo API endpoint, gọi Service
4️⃣  Frontend    → Gọi API, hiển thị UI
```

## 👥 Phân công theo Role

| TV | Backend | Frontend |
|----|---------|----------|
| TV1 | `Controllers/Auth/` + `Services/Auth/` + `Controllers/Seller/` + `Services/Seller/` | `features/auth/` + `features/seller/` |
| Ngô Sỹ Giá (TV2 — Chủ kho vựa) | `Controllers/Depot/` + `Services/Depot/` | `features/depot/` |
| TV3 | `Controllers/Employee/` + `Services/Employee/` + `Controllers/Driver/` + `Services/Driver/` | `features/employee/` + `features/driver/` |
| TV4 | `Controllers/Factory/` + `Services/Factory/` | `features/factory/` |
| TV5 | `Controllers/Admin/` + `Services/Admin/` + Infrastructure setup | `features/admin/` |

## 🚀 Hướng Dẫn Cài Đặt và Chạy Dự Án (Getting Started)

### 1. Yêu cầu hệ thống
- **PostgreSQL** (đã kiểm thử local với PostgreSQL 17; dùng database riêng cho phát triển và test)
- **.NET 8 SDK**
- **Node.js** đáp ứng engine của Vite 8 trong lockfile (khuyến nghị Node 22.12+)

### 2. Cấu hình Database
Tạo database local riêng, ví dụ `Retrack_TV2_dev`. Với máy mới, sao chép `backend/Retrack.API/appsettings.example.json` thành `appsettings.json`, rồi sao chép `.env.example` thành `.env` cùng thư mục và điền thông tin riêng. Nếu đã có cấu hình local, giữ nguyên file hiện có. Cả `appsettings.json` và `.env` đều được Git bỏ qua; chỉ đưa các bản mẫu không có thông tin mật vào commit.

Nguồn schema đang dùng là [`db/depot ower/retrack-system.sql`](db/depot%20ower/retrack-system.sql). Với **database local mới, rỗng**, tạo database rồi chạy file này một lần bằng `psql` hoặc pgAdmin; kiểm tra lại câu lệnh SQL trước khi áp dụng cho database đã có dữ liệu. Ứng dụng hiện **không tự chạy EF migration**. `Database__Initialize=true` chỉ seed dữ liệu demo khi chạy ở Development và schema đã tồn tại; mặc định để `false`. Không bật trên database dùng chung/production. Biến môi trường tiến trình được ưu tiên hơn `.env`.

Các file EF migration cũ vẫn còn trong source để đối chiếu lịch sử, chưa được hợp nhất/xóa. Nhóm sẽ chốt lại DB-first về sau; không tự áp dụng song song migration và script schema lên cùng database.

### 3. Cấu hình Frontend
Mở thư mục `frontend/`, sao chép `.env.example` thành `.env` nếu cần cấu hình riêng.
(Nếu có Client ID của Google để đăng nhập, bạn có thể điền vào `VITE_GOOGLE_CLIENT_ID`).

### 4. Chạy Backend (.NET 8)
```bash
dotnet restore backend/Retrack.sln
dotnet run --project backend/Retrack.API --launch-profile http
# API Endpoint: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```
> API dùng schema đã chuẩn bị trước; `Database__Initialize=true` chỉ seed dữ liệu Development.

### 5. Chạy Frontend (React + Vite)
```bash
cd frontend
npm ci
npm run dev
# Mở trình duyệt tại: http://localhost:5173
```

### 6. Tài Khoản Mẫu (Seed Data)
Khi seed dữ liệu demo ở Development, có tài khoản thử nghiệm cho các role: `admin@retrack.vn`, `seller@retrack.vn`, `depot@retrack.vn`, `employee@retrack.vn`, `driver@retrack.vn` và `factory@retrack.vn`. Người phụ trách chia sẻ mật khẩu thử nghiệm qua kênh riêng; không ghi mật khẩu trong README hoặc commit.

## 📝 Git Workflow
```bash
git checkout -b feature/TV2-ngo-sy-gia-depot-dashboard
# ... code ...
git add <cac-file-cua-task>
git commit -m "feat(depot): implement dashboard API"
# Push / tạo PR khi đã được phép
```

## Kiểm thử và bàn giao Depot

- `dotnet build backend/Retrack.sln --no-restore`
- Tạo riêng `Retrack_TV2_test`, đặt cả `RETRACK_TEST_CONNECTION` và `RETRACK_TEST_POSTGRES` tới database đó, chạy `dotnet test backend/Retrack.sln --no-restore`. Tests PostgreSQL dùng database riêng và chỉ dọn fixture của chúng; không trỏ vào database ứng dụng.
- Trong `frontend`: `npm run lint` và `npm run build`.
- Quy tắc chung: [AGENTS.md](AGENTS.md). Cách chạy kiểm thử trình duyệt Depot–Factory và PayOS giả lập: [frontend/e2e/README.md](frontend/e2e/README.md). Hồ sơ tiến độ cá nhân ở `.ai-context/depot-owner` chỉ tồn tại local, không nằm trong Git.
- Schema PostgreSQL DB-first duy nhất cho toàn hệ thống ở `db/depot ower/retrack-system.sql`; đã gộp schema cơ sở, phần mở rộng Factory và các trường Depot mới. Dùng file này cho database rỗng hoặc để bổ sung cột/index còn thiếu; không chạy thêm file schema nối tiếp. Các file `seed_*.sql` chỉ nạp dữ liệu kiểm thử, không tạo schema. Kiểm thử backend nằm ở `backend/Retrack.Tests` và `backend/Retrack.API.Tests`.
