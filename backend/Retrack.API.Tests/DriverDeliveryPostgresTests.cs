using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Driver;
using Xunit;

namespace Retrack.API.Tests;

public sealed class DriverDeliveryPostgresTests
{
    [PostgresPickupFact]
    public async Task ConcurrentEvidenceRetriesAndPickupVersusCancelAreAtomic()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        var second = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(second)
            || new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test"
            || new NpgsqlConnectionStringBuilder(second).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Cả hai biến test phải trỏ đúng Retrack_TV2_test.");
        var schema = "driver_delivery_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await command.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
        AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
        try
        {
            await using var db = Open();
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            // Bảng fixture còn rỗng: tạo lại bằng SQL triển khai để kiểm chứng cả constraints.
            await db.Database.ExecuteSqlRawAsync("DROP TABLE driver_delivery_events");
            // Kiểm tra cả SQL triển khai thực tế trên schema fixture mới, không chỉ EF model.
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "db", "driver", "03_driver_delivery.sql"))) root = root.Parent;
            Assert.NotNull(root);
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(root!.FullName, "db", "driver", "03_driver_delivery.sql")));
            User NewUser(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.local" };
            var driver = NewUser("DRIVER");
            var depot = new Depot { Owner = NewUser("DEPOT_OWNER"), Latitude = 16.05m, Longitude = 108.2m };
            var factory = new Factory { Owner = NewUser("FACTORY"), Latitude = 16.05m, Longitude = 108.2m };
            db.DepotStaffs.Add(new() { User = driver, Depot = depot, StaffType = "DRIVER" });
            TransportJob NewJob() => new() { Driver = driver, Status = "ACCEPTED", Batch = new() {
                Depot = depot, TargetFactory = factory, MaterialType = "PET", DeclaredWeightKg = 100, Status = "ACCEPTED" } };
            var job = NewJob(); var raceJob = NewJob();
            db.TransportJobs.AddRange(job, raceJob); await db.SaveChangesAsync();
            var images = new EmployeeCollectionTests.TestImages();
            async Task<bool> Attempt(Guid id, string action, Guid operation, Task gate)
            {
                await using var attempt = Open(); await gate;
                var evidence = new EmployeeCheckInRequest { Latitude = 16.05, Longitude = 108.2, AccuracyMeters = 10,
                    PhotoTakenAt = DateTimeOffset.UtcNow, LocationRecordedAt = DateTimeOffset.UtcNow,
                    ImageFile = new FormFile(new MemoryStream(new byte[] {255,216,255,224,0,0,0,0}), 0, 8, "imageFile", "test.jpg") };
                try {
                    await new DriverDeliveryService(attempt, new(attempt), images).ExecuteAsync(driver.Id, id, action,
                        new(operation, action == "cancel" ? "Xe chưa thể tiếp tục nhận hàng" : null), evidence, default);
                    return true;
                } catch (DepotConflictException) { return false; }
                catch (KeyNotFoundException) { return false; }
            }
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var key = Guid.NewGuid();
            var a = Attempt(job.Id, "checkin", key, gate.Task); var b = Attempt(job.Id, "checkin", key, gate.Task); gate.SetResult();
            Assert.All(await Task.WhenAll(a, b), Assert.True);
            Assert.Equal(1, images.UploadCount);
            await using (var verify = Open()) {
                Assert.Single(await verify.DriverDeliveryEvents.ToListAsync());
                Assert.Single(await verify.Notifications.ToListAsync());
            }
            gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            a = Attempt(raceJob.Id, "checkin", Guid.NewGuid(), gate.Task);
            b = Attempt(raceJob.Id, "cancel", Guid.NewGuid(), gate.Task); gate.SetResult();
            Assert.Single(await Task.WhenAll(a, b), success => success);
            await using var final = Open();
            var saved = await final.TransportJobs.Include(j => j.Batch).SingleAsync(j => j.Id == raceJob.Id);
            Assert.Single(await final.DriverDeliveryEvents.Where(e => e.JobId == raceJob.Id).ToListAsync());
            if (saved.Status == "PENDING") { Assert.Null(saved.DriverId); Assert.Equal("ACCEPTED", saved.Batch.Status); }
            else { Assert.Equal("PICKED_UP", saved.Status); Assert.Equal(driver.Id, saved.DriverId); Assert.Equal("IN_TRANSIT", saved.Batch.Status); }
        }
        finally {
            // Chỉ dọn schema UUID tạo bởi chính fixture trong database test đã xác minh.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
