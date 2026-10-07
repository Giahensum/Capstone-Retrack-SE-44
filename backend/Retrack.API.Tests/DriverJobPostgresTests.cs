using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Driver;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.API.Tests;

public sealed class DriverJobPostgresTests
{
    [PostgresPickupFact]
    public async Task ConcurrentDriversHaveOneWinnerAndOneOwnerNotice()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var otherConnection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test"
            || string.IsNullOrWhiteSpace(otherConnection) || new NpgsqlConnectionStringBuilder(otherConnection).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Chỉ chạy khi cả hai biến test trỏ đúng Retrack_TV2_test.");
        var schema = "driver_job_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
        AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
        try
        {
            await using var db = Open();
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            User NewUser(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.local" };
            var depot = new Depot { Owner = NewUser("DEPOT_OWNER") };
            var first = NewUser("DRIVER"); var second = NewUser("DRIVER");
            db.DepotStaffs.AddRange(new DepotStaff { User = first, Depot = depot, StaffType = "DRIVER" }, new DepotStaff { User = second, Depot = depot, StaffType = "DRIVER" });
            var job = new TransportJob { Batch = new() { Depot = depot, Status = "ACCEPTED", MaterialType = "PET", DeclaredWeightKg = 100,
                TargetFactory = new() { Owner = NewUser("FACTORY") } } };
            db.TransportJobs.Add(job); await db.SaveChangesAsync();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Accept(Guid id)
            {
                await using var attempt = Open(); await start.Task;
                try { await new DriverJobService(attempt).AcceptAsync(id, job.Id, default); return true; }
                catch (DepotConflictException) { return false; }
            }
            var a = Accept(first.Id); var b = Accept(second.Id); start.SetResult();
            Assert.Single(await Task.WhenAll(a, b), won => won);
            await using var verify = Open();
            var saved = await verify.TransportJobs.SingleAsync();
            Assert.Equal("ACCEPTED", saved.Status); Assert.NotNull(saved.DriverId);
            Assert.Equal(depot.OwnerId, (await verify.Notifications.SingleAsync()).UserId);
            await new DriverJobService(verify).AcceptAsync(saved.DriverId!.Value, job.Id, default);
            Assert.Single(await verify.Notifications.ToListAsync());
        }
        finally
        {
            // Chỉ dọn schema fixture UUID vừa tạo, không xóa schema dữ liệu ứng dụng.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
