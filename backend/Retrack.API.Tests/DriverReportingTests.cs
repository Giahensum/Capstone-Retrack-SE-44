using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Driver;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;
using Xunit;

namespace Retrack.API.Tests;
public sealed class DriverReportingTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private DriverReportingService service = null!;
    private static User UserFor(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.local" };
    private readonly User driver = UserFor("DRIVER"), other = UserFor("DRIVER");
    private readonly Depot depot = new() { Name = "Kho", Owner = UserFor("DEPOT_OWNER") };
    private readonly Factory factory = new() { Name = "Nhà máy", Owner = UserFor("FACTORY") };
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.DepotStaffs.AddRange(new DepotStaff { User = driver, Depot = depot, StaffType = "DRIVER" },
            new DepotStaff { User = other, Depot = depot, StaffType = "DRIVER" });
        db.Factories.Add(factory);
        await db.SaveChangesAsync();
        service = new(db, new(db));
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private TransportJob Add(string state = "DELIVERED", Guid? who = null, Depot? at = null)
    {
        var job = new TransportJob { DriverId = state == "PENDING" ? null : who ?? driver.Id, Status = state,
            Batch = new() { Depot = at ?? depot, TargetFactory = factory, Status = state == "PENDING" ? "TRANSPORT_READY" : state,
                MaterialType = "PET", DeclaredWeightKg = 123.45m } };
        db.TransportJobs.Add(job); return job;
    }
    private void Event(TransportJob job, string action, DateTime when) => db.DriverDeliveryEvents.Add(new()
        { Job = job, DriverId = driver.Id, OperationId = Guid.NewGuid(), Action = action, CreatedAt = when });
    [Fact]
    public async Task EmptyReportsAndInputValidation()
    {
        Assert.Equal(0, (await service.StatsAsync(driver.Id, default)).AllTime.Trips);
        Assert.Empty((await service.DashboardAsync(driver.Id, default)).ActivePreview);
        await Assert.ThrowsAsync<ArgumentException>(() => service.HistoryAsync(driver.Id, 0, 20, "all", "all", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.HistoryAsync(driver.Id, 1, 51, "all", "all", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.HistoryAsync(driver.Id, 1, 20, "week", "all", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.HistoryAsync(driver.Id, 1, 20, "all", "bad", default));
    }
    [Fact]
    public async Task ScopeFiltersAndReceiptDoNotCountAnotherDriverOrDepot()
    {
        var own = Add(); own.Batch.FactoryReceivedAt = DateTime.UtcNow;
        Add(); Add(who: other.Id);
        Add(at: new Depot { Owner = UserFor("DEPOT_OWNER") });
        Add("IN_TRANSIT");
        await db.SaveChangesAsync();
        var totals = (await service.StatsAsync(driver.Id, default)).AllTime;
        Assert.Equal(2, totals.Trips); Assert.Equal(246.9, totals.DeclaredWeightKg, 3); Assert.Equal(1, totals.FactoryReceived);
        Assert.Single((await service.HistoryAsync(driver.Id, 1, 20, "all", "received", default)).Items);
        Assert.Single((await service.HistoryAsync(driver.Id, 1, 20, "all", "waiting", default)).Items);
        Assert.Equal(1, (await service.DashboardAsync(driver.Id, default)).ActiveJobs);
    }
    [Fact]
    public async Task CheckoutDateWinsOverLaterUpdatesAndMonthUsesVietnamTime()
    {
        var start = EmployeeReportingService.VietnamMonthStart(DateTime.UtcNow);
        var old = Add(); Event(old, "checkout", start.AddTicks(-1));
        var current = Add(); Event(current, "checkout", start);
        // Không đếm sự kiện hai lần nếu dữ liệu nhập cũ có nhiều checkout.
        Event(current, "checkout", start.AddSeconds(1));
        await db.SaveChangesAsync();
        Assert.Equal(1, (await service.StatsAsync(driver.Id, default)).ThisMonth.Trips);
        var row = Assert.Single((await service.HistoryAsync(driver.Id, 1, 20, "month", "all", default)).Items);
        Assert.False(row.LegacyDate); Assert.Equal(start.AddSeconds(1), row.DeliveredAt);
    }
    [Fact]
    public async Task LegacyPaginationAndInactiveMembership()
    {
        Add(); Add(); Add(); await db.SaveChangesAsync();
        var first = await service.HistoryAsync(driver.Id, 1, 2, "all", "all", default);
        var second = await service.HistoryAsync(driver.Id, 2, 2, "all", "all", default);
        Assert.Equal(3, first.TotalCount); Assert.Equal(2, first.Items.Count); Assert.Single(second.Items);
        Assert.All(first.Items, j => Assert.True(j.LegacyDate));
        Assert.DoesNotContain(second.Items[0].Id, first.Items.Select(j => j.Id));
        driver.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.StatsAsync(driver.Id, default));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.DashboardAsync(driver.Id, default));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.HistoryAsync(driver.Id, 1, 20, "all", "all", default));
    }
    [Fact]
    public async Task MultipleDepotsAreIncludedButRevokedMembershipAndWrongRoleAreExcluded()
    {
        var second = new Depot { Owner = UserFor("DEPOT_OWNER") };
        var membership = new DepotStaff { User = driver, Depot = second, StaffType = "DRIVER" };
        db.DepotStaffs.Add(membership);
        Add(); Add(at: second); await db.SaveChangesAsync();
        Assert.Equal(2, (await service.StatsAsync(driver.Id, default)).AllTime.Trips);
        membership.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(1, (await service.StatsAsync(driver.Id, default)).AllTime.Trips);
        driver.Role = "DEPOT_EMPLOYEE"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.StatsAsync(driver.Id, default));
    }
    [Fact]
    public async Task CheckoutKeepsTodayCountStableAfterJobUpdateAndCancelIsNotDelivered()
    {
        var job = Add(); job.UpdatedAt = DateTime.UtcNow.AddDays(-10);
        Event(job, "checkout", DateTime.UtcNow);
        var cancelled = Add("PENDING"); Event(cancelled, "cancel", DateTime.UtcNow);
        Add("CANCELLED"); await db.SaveChangesAsync();
        var dashboard = await service.DashboardAsync(driver.Id, default);
        Assert.Equal(1, dashboard.DeliveredToday); Assert.Equal(1, dashboard.AwaitingFactory);
        Assert.Equal(1, (await service.StatsAsync(driver.Id, default)).AllTime.Trips);
        job.Batch.FactoryReceivedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        Assert.Equal(0, (await service.DashboardAsync(driver.Id, default)).AwaitingFactory);
        Assert.Equal(1, (await service.StatsAsync(driver.Id, default)).AllTime.Trips);
    }
    [Fact]
    public async Task DashboardMatchesPoolAndIgnoresRejectedJobsAndForeignNotices()
    {
        Add("PENDING"); var rejected = Add("PENDING"); Event(rejected, "reject", DateTime.UtcNow);
        Add("ACCEPTED"); Add("PICKED_UP"); Add("IN_TRANSIT"); Add("ON_THE_WAY");
        db.Notifications.Add(new() { UserId = driver.Id, Title = "Test" });
        db.Notifications.Add(new() { UserId = other.Id, Title = "Test" });
        await db.SaveChangesAsync();
        var dashboard = await service.DashboardAsync(driver.Id, default);
        Assert.Equal((await new DriverJobService(db).ListAsync(driver.Id, false, 1, default)).TotalCount, dashboard.AvailableJobs);
        Assert.Equal(1, dashboard.AvailableJobs); Assert.Equal(4, dashboard.ActiveJobs);
        Assert.Equal(3, dashboard.ActivePreview.Count); Assert.Equal(1, dashboard.UnreadNotifications);
    }
}
