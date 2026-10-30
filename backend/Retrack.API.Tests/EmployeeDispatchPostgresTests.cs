using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Employee;
using Xunit;

namespace Retrack.API.Tests;

public sealed class EmployeeDispatchPostgresTests
{
    [PostgresPickupFact]
    public async Task BootstrapUpgradeAndDanangSeedRunTwiceAndDispatchWithoutChangingCompletedOrders()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        Assert.Equal("Retrack_TV2_test",new NpgsqlConnectionStringBuilder(connection).Database);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName,"db","bootstrap"))) root = root.Parent;
        Assert.NotNull(root);
        var schema = "dispatch_seed_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"CREATE SCHEMA {schema}",admin)) await cmd.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
        try
        {
            await using var sql = new NpgsqlConnection(scoped); await sql.OpenAsync();
            async Task Run(string file)
            {
                await using var cmd = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root!.FullName,file)),sql);
                await cmd.ExecuteNonQueryAsync();
            }
            await Run("db/bootstrap/retrack-system.sql");
            await Run("db/bootstrap/retrack-system.sql");
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
            var depotId = Guid.Parse("ed200000-0000-4000-8000-000000000001");
            Assert.Equal(36,await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId));
            Assert.Equal(6,await db.EmployeeCollectionAreas.CountAsync());
            Assert.Equal(6,await db.EmployeeDispatchShifts.CountAsync());
            var completed = await db.PickupRequests.AsNoTracking().Where(p => p.Status == "DONE")
                .Select(p => new {p.Id,p.AcceptedCollectorId,p.GrossAmount,p.NetAmount}).OrderBy(p => p.Id).ToListAsync();
            var dayEnd = DateTime.UtcNow.AddHours(7).Date.AddDays(1).AddHours(-7);
            var eligible = await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId
                && (p.PreferredDatetime == null || p.PreferredDatetime < dayEnd));
            await new EmployeeDispatchService(db).DispatchAsync(default);
            Assert.Equal(eligible,await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId && p.Status == "SCHEDULED"));
            Assert.Equal(completed,await db.PickupRequests.AsNoTracking().Where(p => p.Status == "DONE")
                .Select(p => new {p.Id,p.AcceptedCollectorId,p.GrossAmount,p.NetAmount}).OrderBy(p => p.Id).ToListAsync());
            var count = await db.EmployeeDispatchEvents.CountAsync();
            await Run("db/bootstrap/retrack-system.sql");
            await new EmployeeDispatchService(db).DispatchAsync(default);
            Assert.Equal(count,await db.EmployeeDispatchEvents.CountAsync());
            Assert.Equal(36,await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [PostgresPickupFact]
    public async Task ConcurrentDispatchAndPauseKeepOneAssignmentAndDoNotMoveCheckedInOrder()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("Retrack_TV2_test", parsed.Database);
        var schema = "dispatch_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
        AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
        try
        {
            await using var setup = NewDb();
            await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
            User UserFor(string role) => new() { Email = $"{Guid.NewGuid():N}@test.local", Role = role };
            var first = UserFor("DEPOT_EMPLOYEE"); var second = UserFor("DEPOT_EMPLOYEE");
            var depot = new Depot { Owner = UserFor("DEPOT_OWNER"), VerificationStatus = "APPROVED" };
            setup.DepotStaffs.AddRange(new DepotStaff { User = first, Depot = depot, StaffType = first.Role },
                new DepotStaff { User = second, Depot = depot, StaffType = second.Role });
            var area = new EmployeeCollectionArea { DepotId = depot.Id, Name = "Test", Latitude = 16.05, Longitude = 108.22 };
            setup.EmployeeCollectionAreas.Add(area);
            var shift = new EmployeeDispatchShift { AreaId = area.Id, EmployeeId = first.Id,
                StartsAt = DateTime.UtcNow.AddMinutes(-1), EndsAt = DateTime.UtcNow.AddHours(1) };
            setup.EmployeeDispatchShifts.Add(shift);
            var pickup = new PickupRequest { Seller = UserFor("SELLER"), TargetDepot = depot,
                Address = "Test", Latitude = 16.05m, Longitude = 108.22m };
            setup.PickupRequests.Add(pickup);
            await setup.SaveChangesAsync();
            async Task Dispatch() { await using var db = NewDb(); await new EmployeeDispatchService(db).DispatchAsync(default); }
            await Task.WhenAll(Dispatch(), Dispatch());
            Assert.Single(await setup.EmployeeDispatchEvents.ToListAsync());
            Assert.Equal(2,await setup.Notifications.CountAsync());
            Assert.Equal(first.Id,(await setup.PickupRequests.AsNoTracking().SingleAsync()).AcceptedCollectorId);
            setup.EmployeeDispatchShifts.Add(new EmployeeDispatchShift { AreaId = area.Id, EmployeeId = second.Id,
                StartsAt = shift.StartsAt, EndsAt = shift.EndsAt });
            await setup.SaveChangesAsync();
            async Task Pause()
            {
                await using var db = NewDb();
                await new EmployeeDispatchService(db).ChangeAvailabilityAsync(first.Id, shift.Id,
                    new("FULL","Xe đầy cần về kho bàn giao"),default);
            }
            await Task.WhenAll(Pause(), Dispatch());
            Assert.Equal(second.Id,(await setup.PickupRequests.AsNoTracking().SingleAsync()).AcceptedCollectorId);
            Assert.Equal(2,await setup.EmployeeDispatchEvents.CountAsync());
            await using var proofDb = NewDb();
            await new EmployeeCollectionService(proofDb,new EmployeeCollectionTests.TestImages())
                .CheckInAsync(second.Id,pickup.Id,new() {
                    PhotoTakenAt = DateTimeOffset.UtcNow,
                    ImageFile = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(new byte[] {255,216,255,224,0,0,0,0}),
                        0,8,"imageFile","proof.jpg")
                },default);
            var secondShift = await setup.EmployeeDispatchShifts.AsNoTracking().SingleAsync(s => s.EmployeeId == second.Id);
            await using var pauseDb = NewDb();
            await new EmployeeDispatchService(pauseDb).ChangeAvailabilityAsync(second.Id,secondShift.Id,
                new("STOPPED","Không thể tiếp tục ca thu gom"),default);
            var final = await setup.PickupRequests.AsNoTracking().SingleAsync();
            Assert.Equal(second.Id,final.AcceptedCollectorId); Assert.Equal("IN_PROGRESS",final.Status);
            Assert.Equal(2,await setup.EmployeeDispatchEvents.CountAsync());
        }
        finally
        {
            // Chỉ xóa schema fixture do ca kiểm thử này tạo trong DB kiểm thử đã xác minh tên.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
