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
│       │   ├── Depot/              ← TV2: Dashboard, inventory, batch, payment, staff
│       │   ├── Employee/           ← TV3: Accept order, check-in, sort & weigh
│       │   ├── Driver/             ← TV3: Accept job, check-in/out
│       │   ├── Factory/            ← TV4: Marketplace, QC, settlement, partner
│       │   ├── Admin/              ← TV5: User management, fee config
│       │   └── Shared/             ← Notification, image upload
│       ├── Services/               ← 🧠 Business Logic (chia theo Role)
│       │   ├── Interfaces/         ← Interface definitions
│       │   ├── Auth/               ← TV1
│       │   ├── Seller/             ← TV1
│       │   ├── Depot/              ← TV2
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
│       │   ├── depot/              ← TV2: Dashboard, Inventory, Staff...
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
| TV2 | `Controllers/Depot/` + `Services/Depot/` | `features/depot/` |
| TV3 | `Controllers/Employee/` + `Services/Employee/` + `Controllers/Driver/` + `Services/Driver/` | `features/employee/` + `features/driver/` |
| TV4 | `Controllers/Factory/` + `Services/Factory/` | `features/factory/` |
| TV5 | `Controllers/Admin/` + `Services/Admin/` + Infrastructure setup | `features/admin/` |

## 🚀 Getting Started

### Yêu cầu cài đặt

- Git
- Node.js LTS (kèm npm)
- .NET SDK 8
- Docker Desktop (dùng để chạy PostgreSQL development)

### Khởi tạo database sau khi clone

Mở Docker Desktop, sau đó chạy tại thư mục gốc của dự án:

```powershell
pwsh -ExecutionPolicy Bypass -File scripts/init-db.ps1
```

Lệnh trên sẽ:

1. Khởi động PostgreSQL bằng Docker Compose.
2. Tạo database `retrack` nếu chưa tồn tại.
3. Tự động nạp schema trong `database/init/001_schema.sql` ở lần khởi động đầu tiên.
4. Kiểm tra database đã có bảng trước khi báo hoàn tất.

Muốn xóa database development và tạo lại hoàn toàn:

```powershell
pwsh -ExecutionPolicy Bypass -File scripts/init-db.ps1 -Reset
```

> `-Reset` sẽ xóa Docker volume và toàn bộ dữ liệu development hiện tại.

### Backend
```bash
cd backend/Retrack.API
dotnet restore
dotnet run
# API: https://localhost:5001
# Swagger: https://localhost:5001/swagger
```

### Frontend
```bash
cd fe
npm ci
npm run dev
# App: http://localhost:5173
```

## 📝 Git Workflow
```bash
git checkout -b feature/TV2-depot-dashboard    # Tạo branch theo TV + feature
# ... code ...
git add .
git commit -m "feat(depot): implement dashboard API"
git push origin feature/TV2-depot-dashboard
# → Tạo Pull Request trên GitHub
```
