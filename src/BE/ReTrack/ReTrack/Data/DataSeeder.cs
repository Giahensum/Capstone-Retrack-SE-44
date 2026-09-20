using Microsoft.EntityFrameworkCore;
using ReTrack.Models;

namespace ReTrack.Data
{
    /// <summary>
    /// Seeds mặc định tài khoản cho từng role để dev/test
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(AppDbContext db)
        {
            // Chỉ seed khi bảng users còn trống
            if (await db.Users.AnyAsync()) return;

            // ── Seed Users ────────────────────────────────────────────────
            var adminUser = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Email = "admin@retrack.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = "ADMIN",
                FullName = "Admin ReTrack",
                Phone = "0900000001",
                IsActive = true
            };

            var sellerUser = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Email = "seller@retrack.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Seller@123"),
                Role = "SELLER",
                FullName = "Nguyễn Thị Lan",
                Phone = "0911111111",
                IsActive = true
            };

            var depotOwnerUser = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Email = "depot@retrack.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Depot@123"),
                Role = "DEPOT_OWNER",
                FullName = "Trần Văn Bình",
                Phone = "0922222222",
                IsActive = true
            };

            var employeeUser = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Email = "employee@retrack.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                Role = "DEPOT_EMPLOYEE",
                FullName = "Lê Minh Tuấn",
                Phone = "0933333333",
                IsActive = true
            };

            var driverUser = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                Email = "driver@retrack.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Driver@123"),
                Role = "DRIVER",
                FullName = "Phạm Văn Hùng",
                Phone = "0944444444",
                IsActive = true
            };

            var factoryUser = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                Email = "factory@retrack.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Factory@123"),
                Role = "FACTORY",
                FullName = "Eco Plastics VN",
                Phone = "0955555555",
                IsActive = true
            };

            db.Users.AddRange(adminUser, sellerUser, depotOwnerUser, employeeUser, driverUser, factoryUser);

            // ── Seed Depot ────────────────────────────────────────────────
            var depot = new Depot
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                OwnerId = depotOwnerUser.Id,
                Name = "Kho Vựa Phế Liệu Minh Bình",
                Address = "123 Đường Lý Thường Kiệt, Phường 7, Quận Tân Bình, TP.HCM",
                Latitude = 10.8005m,
                Longitude = 106.6637m,
                Rating = 4.5m
            };
            db.Depots.Add(depot);

            // ── Seed DepotStaff ───────────────────────────────────────────
            db.DepotStaffs.AddRange(
                new DepotStaff
                {
                    Id = Guid.NewGuid(),
                    DepotId = depot.Id,
                    UserId = employeeUser.Id,
                    StaffType = "DEPOT_EMPLOYEE",
                    IsActive = true
                },
                new DepotStaff
                {
                    Id = Guid.NewGuid(),
                    DepotId = depot.Id,
                    UserId = driverUser.Id,
                    StaffType = "DRIVER",
                    IsActive = true
                }
            );

            // ── Seed Factory ──────────────────────────────────────────────
            var factory = new Factory
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                OwnerId = factoryUser.Id,
                Name = "Eco Plastics Vietnam",
                Address = "45 Khu Công Nghiệp Tân Bình, TP.HCM",
                Latitude = 10.8234m,
                Longitude = 106.6501m,
                Rating = 4.2m
            };
            db.Factories.Add(factory);

            // ── Seed FactoryDemand ────────────────────────────────────────
            db.FactoryDemands.AddRange(
                new FactoryDemand
                {
                    Id = Guid.NewGuid(),
                    FactoryId = factory.Id,
                    MaterialType = "Nhựa PET",
                    RequiredWeightKg = 5000,
                    MinPricePerKg = 8000,
                    MaxPricePerKg = 12000,
                    Deadline = DateTime.UtcNow.AddDays(30),
                    IsActive = true
                },
                new FactoryDemand
                {
                    Id = Guid.NewGuid(),
                    FactoryId = factory.Id,
                    MaterialType = "Sắt vụn",
                    RequiredWeightKg = 10000,
                    MinPricePerKg = 12000,
                    MaxPricePerKg = 15000,
                    Deadline = DateTime.UtcNow.AddDays(15),
                    IsActive = true
                }
            );

            await db.SaveChangesAsync();
        }
    }
}
