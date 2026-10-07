using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Driver;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.API.Tests;

public sealed class DriverJobTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private static User UserFor(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.local" };
    private readonly User driver = UserFor("DRIVER"), other = UserFor("DRIVER");
    private readonly Depot depot = new() { Name = "Kho", Owner = UserFor("DEPOT_OWNER") };
    private TransportJob job = null!;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.DepotStaffs.AddRange(new DepotStaff { User = driver, Depot = depot, StaffType = "DRIVER" }, new DepotStaff { User = other, Depot = depot, StaffType = "DRIVER" });
        job = new() { Batch = new() { Depot = depot, Status = "TRANSPORT_READY", MaterialType = "PET", DeclaredWeightKg = 100,
            TargetFactory = new() { Name = "Nhà máy", Owner = UserFor("FACTORY") } } };
        db.TransportJobs.Add(job); await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    [Fact]
    public async Task PoolAcceptRetryAndCompetingDriver()
    {
        var service = new DriverJobService(db);
        Assert.Single((await service.ListAsync(driver.Id, false, 1, default)).Items);
        Assert.Equal("ACCEPTED", (await service.AcceptAsync(driver.Id, job.Id, default)).Status);
        await service.AcceptAsync(driver.Id, job.Id, default);
        Assert.Single(await db.Notifications.ToListAsync());
        await Assert.ThrowsAsync<DepotConflictException>(() => service.AcceptAsync(other.Id, job.Id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DetailAsync(other.Id, job.Id, default));
        Assert.Empty((await service.ListAsync(driver.Id, false, 1, default)).Items);
        Assert.Single((await service.ListAsync(driver.Id, true, 1, default)).Items);
        Assert.Equal("TRANSPORT_READY", job.Batch.Status);
    }
    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("PENDING_APPROVAL")]
    [InlineData("DELIVERED")]
    public async Task InvalidBatchNeverEntersPool(string status)
    {
        job.Batch.Status = status; await db.SaveChangesAsync();
        var service = new DriverJobService(db);
        Assert.Empty((await service.ListAsync(driver.Id, false, 1, default)).Items);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.AcceptAsync(driver.Id, job.Id, default));
        Assert.Empty(await db.Notifications.ToListAsync());
    }
    [Fact]
    public async Task WrongDepotAndInactiveAccountCannotAccessJobs()
    {
        var stranger = UserFor("DRIVER");
        db.DepotStaffs.Add(new() { User = stranger, Depot = new() { Owner = UserFor("DEPOT_OWNER") }, StaffType = "DRIVER" });
        await db.SaveChangesAsync();
        var service = new DriverJobService(db);
        Assert.Empty((await service.ListAsync(stranger.Id, false, 1, default)).Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AcceptAsync(stranger.Id, job.Id, default));
        driver.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.ListAsync(driver.Id, false, 1, default));
    }
    [Fact]
    public async Task NoticeRecipientsAreActiveDriversOfThisDepotOnly()
    {
        other.IsActive = false; await db.SaveChangesAsync();
        await DriverJobNotices.QueueAsync(db, depot.Id, job.Id); await db.SaveChangesAsync();
        var notice = await db.Notifications.SingleAsync();
        Assert.Equal(driver.Id, notice.UserId); Assert.Equal(job.Id, notice.TransportJobId);
    }
    [Fact]
    public async Task MissingFactoryAndInvalidPaginationAreRejected()
    {
        job.Batch.TargetFactory = null; job.Batch.TargetFactoryId = null; await db.SaveChangesAsync();
        var service = new DriverJobService(db);
        Assert.Empty((await service.ListAsync(driver.Id, false, 1, default)).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(driver.Id, false, 0, default));
        await Assert.ThrowsAsync<DepotConflictException>(() => service.AcceptAsync(driver.Id, job.Id, default));
    }
}
