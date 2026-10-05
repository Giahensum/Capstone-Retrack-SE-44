using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Interfaces;
using Xunit;

namespace Retrack.API.Tests;

public sealed class EmployeeCollectionPostgresTests
{
    [PostgresPickupFact]
    public async Task ConcurrentCheckInAndDraftEditsAreSerializedAndPersisted()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var other = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test"
            || string.IsNullOrWhiteSpace(other) || new NpgsqlConnectionStringBuilder(other).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Chỉ chạy trên Retrack_TV2_test với cả hai biến RETRACK_TEST_POSTGRES và RETRACK_TEST_CONNECTION.");
        var schema = "collection_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
        AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
        try
        {
            await using var setup = Open();
            await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
            User NewUser(string role) => new() { Role = role, Email = $"{Guid.NewGuid():N}@test.local" };
            var employee = NewUser("DEPOT_EMPLOYEE");
            var depot = new Depot { Owner = NewUser("DEPOT_OWNER"), Name = "Kho test" };
            var pickup = new PickupRequest { Seller = NewUser("SELLER"), TargetDepot = depot, AcceptedCollector = employee,
                Address = "Test", Latitude = 16m, Longitude = 108m, Status = "SCHEDULED" };
            setup.DepotStaffs.Add(new DepotStaff { Depot = depot, User = employee, StaffType = employee.Role });
            setup.PickupRequests.Add(pickup); await setup.SaveChangesAsync();
            var images = new ConcurrentImages();
            async Task<EmployeeCollectionDto> CheckIn()
            {
                await using var db = Open();
                return await new EmployeeCollectionService(db, images).CheckInAsync(employee.Id, pickup.Id, new()
                {
                    Latitude = 16, Longitude = 108, AccuracyMeters = 5, PhotoTakenAt = DateTimeOffset.UtcNow,
                    LocationRecordedAt = DateTimeOffset.UtcNow,
                    ImageFile = new FormFile(new MemoryStream(new byte[] { 255, 216, 255, 0, 0, 0, 0, 0 }), 0, 8, "imageFile", "test.jpg")
                }, default);
            }
            var results = await Task.WhenAll(CheckIn(), CheckIn());
            Assert.All(results, r => Assert.Equal("IN_PROGRESS", r.Status));
            Assert.Equal(1, images.Count);
            Assert.Single(await setup.Notifications.AsNoTracking().ToListAsync());
            Assert.Single(await setup.PickupCheckIns.AsNoTracking().ToListAsync());
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Save(decimal weight)
            {
                await using var db = Open(); await start.Task;
                try
                {
                    await new EmployeeCollectionService(db, images).SaveAsync(employee.Id, pickup.Id,
                        new(0, [new("PET", weight, 5000)]), default);
                    return true;
                }
                catch (DepotConflictException) { return false; }
            }
            var first = Save(1); var second = Save(2); start.SetResult();
            Assert.Single(await Task.WhenAll(first, second), success => success);
            await using var verify = Open();
            Assert.Equal(1, (await verify.PickupCheckIns.SingleAsync()).Revision);
            Assert.Single(await verify.PickupRequestItems.ToListAsync());
            Assert.Equal("IN_PROGRESS", (await verify.PickupRequests.SingleAsync()).Status);
            async Task Submit()
            {
                await using var context = Open();
                await new EmployeeCollectionService(context, images).TransitionAsync(employee.Id, pickup.Id, "SUBMITTED", 1, default);
            }
            await Task.WhenAll(Submit(), Submit());
            Assert.Single(await verify.EmployeeCollectionEvents.AsNoTracking().ToListAsync());
            Assert.Equal("WEIGHED", (await verify.PickupRequests.AsNoTracking().SingleAsync()).Status);
            var reports = new EmployeeReportingService(verify);
            Assert.Equal(0, (await reports.StatsAsync(employee.Id, default)).AllTime.Pickups);
            // Fixture xác nhận hoàn tất để kiểm chứng SUM PostgreSQL; không gọi API tài chính role khác.
            await verify.PickupRequests.ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "DONE"));
            var totals = (await reports.StatsAsync(employee.Id, default)).AllTime;
            Assert.Equal(1, totals.Pickups);
            Assert.True(totals.WeightKg > 0); Assert.Equal(totals.WeightKg * 5000, totals.GrossAmount);
        }
        finally
        {
            // Chỉ dọn schema fixture có UUID của bài test này.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class ConcurrentImages : ICloudinaryService
    {
        public int Count;
        public async Task<string> UploadImageAsync(Stream stream, string name)
        { Interlocked.Increment(ref Count); await Task.Delay(100); return "https://test.invalid/checkin.jpg"; }
        public Task<string> UploadAvatarAsync(Stream stream, string name) => throw new NotSupportedException();
        public Task<bool> DeleteImageAsync(string id) => Task.FromResult(true);
    }
}
