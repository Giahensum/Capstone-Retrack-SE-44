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

## 🚀 Hướng Dẫn Cài Đặt và Chạy Dự Án (Getting Started)

### 1. Yêu cầu hệ thống
- **PostgreSQL** (cài đặt và tạo sẵn database tên `ReNATS_DB`)
- **.NET 8 SDK**
- **Node.js** (v18 trở lên)

### 2. Cấu hình Database
Có 2 cách để khởi tạo database:
- **Cách 1**: Chạy file `doc/init_postgres.sql` trong pgAdmin hoặc psql command line.
- **Cách 2**: Để Entity Framework Core tự động migrate khi chạy Backend lần đầu.

Tiếp theo, mở file `src/BE/ReTrack/ReTrack/appsettings.json` và cập nhật mật khẩu PostgreSQL của bạn:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=ReNATS_DB;Username=postgres;Password=MAT_KHAU_CUA_BAN"
}
```

### 3. Cấu hình Frontend
Mở thư mục `src/FE/`, đổi tên file `.env.example` thành `.env`. 
(Nếu có Client ID của Google để đăng nhập, bạn có thể điền vào `VITE_GOOGLE_CLIENT_ID`).

### 4. Chạy Backend (.NET 8)
```bash
cd "src/BE/ReTrack/ReTrack"
dotnet restore
dotnet run
# API Endpoint: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```
> Khi BE chạy lần đầu, nó sẽ tự động chạy DB Migrations và tạo (seed) dữ liệu mẫu.

### 5. Chạy Frontend (React + Vite)
```bash
cd "src/FE"
npm install
npm run dev
# Mở trình duyệt tại: http://localhost:5173
```

### 6. Tài Khoản Mẫu (Seed Data)
Hệ thống đã tạo sẵn 6 tài khoản để test cho 6 role. **Mật khẩu chung cho tất cả là**: `<TênRole>@123`
- **Admin**: `admin@retrack.vn` / `Admin@123`
- **Seller**: `seller@retrack.vn` / `Seller@123`
- **Depot Owner**: `depot@retrack.vn` / `Depot@123`
- **Depot Employee**: `employee@retrack.vn` / `Employee@123`
- **Driver**: `driver@retrack.vn` / `Driver@123`
- **Factory**: `factory@retrack.vn` / `Factory@123`

## 📝 Git Workflow
```bash
git checkout -b feature/TV2-depot-dashboard    # Tạo branch theo TV + feature
# ... code ...
git add .
git commit -m "feat(depot): implement dashboard API"
git push origin feature/TV2-depot-dashboard
# → Tạo Pull Request trên GitHub
```
