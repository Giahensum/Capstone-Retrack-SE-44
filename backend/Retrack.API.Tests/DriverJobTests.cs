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
    public async Task NoticesArePrivateAndCannotMarkAnotherDriversNoticeRead()
    {
        var mine = new Notification { UserId = driver.Id, TransportJobId = job.Id, Title = "Của tôi" };
        var foreign = new Notification { UserId = other.Id, TransportJobId = job.Id, Title = "Người khác" };
        db.Notifications.AddRange(mine, foreign); await db.SaveChangesAsync();
        var controller = new Retrack.API.Controllers.Driver.DriverNoticeController(db, new(db))
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, driver.Id.ToString()) }, "test"))
                }
            }
        };
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.List(1));
        var response = Assert.IsType<Retrack.API.DTOs.ApiResponse<object>>(result.Value);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(response.Data);
        Assert.Equal(1, json.GetProperty("unreadCount").GetInt32());
        Assert.Equal(mine.Id, json.GetProperty("items")[0].GetProperty("Id").GetGuid());
        Assert.Equal(job.Id, json.GetProperty("items")[0].GetProperty("jobId").GetGuid());
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>(await controller.Read(foreign.Id, default));
        await controller.Read(mine.Id, default); await controller.Read(mine.Id, default);
        Assert.False((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == foreign.Id)).IsRead);
        Assert.True((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == mine.Id)).IsRead);
        driver.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => controller.List(1));
    }
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
