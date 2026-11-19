using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;
using Xunit;

namespace Retrack.API.Tests;

public sealed class EmployeeDailyPlanTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Depot depot = new() { Owner = User("DEPOT_OWNER"), VerificationStatus = "APPROVED" };
    private readonly User seller = User("SELLER");
    private static User User(string role) => new() { Role = role, Email = $"{Guid.NewGuid():N}@test.invalid" };
    private EmployeeDailyPlanService Planner => new(new(db), new EmployeeDispatchRepository(db), new StaffUnitOfWork(db));
    private EmployeeDispatchService Dispatch => StaffTestServices.Dispatch(db);
    public async Task InitializeAsync()
    {
        await connection.OpenAsync(); db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); db.Users.Add(seller);
        for (var i = 0; i < 3; i++) db.DepotStaffs.Add(new() { Depot = depot, User = User("DEPOT_EMPLOYEE"), StaffType = "DEPOT_EMPLOYEE" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private async Task AddOrders(int count, bool future = false, bool remote = false)
    {
        for (var i = 0; i < count; i++) db.PickupRequests.Add(new() { SellerId = seller.Id, TargetDepotId = depot.Id,
            Address = $"Điểm kiểm thử {Guid.NewGuid()}", Latitude = remote ? 15m : 16.06836m,
            Longitude = remote ? 107m : 108.22443m + i * .000001m, Status = "PENDING",
            PreferredDatetime = future ? DateTime.UtcNow.AddDays(2) : null });
        await db.SaveChangesAsync();
    }
    [Theory]
    [InlineData(14, 1)]
    [InlineData(15, 1)]
    [InlineData(29, 1)]
    [InlineData(75, 1)]
    public async Task Automatically_groups_region_orders_without_splitting_by_count(int count, int people)
    {
        await AddOrders(count); await Planner.PrepareAsync(DateTime.UtcNow, default); await Dispatch.DispatchAsync(default);
        var shifts = await db.EmployeeDispatchShifts.AsNoTracking().ToListAsync();
        Assert.Equal(people, shifts.Count); Assert.All(shifts, s => { Assert.True(s.IsAutomatic); Assert.Equal(50, s.MaxPendingOrders); });
        var assignments = await db.PickupRequests.GroupBy(p => p.AcceptedCollectorId).Select(g => g.Count()).ToListAsync();
        Assert.Equal(people, assignments.Count); Assert.True(assignments.Max() - assignments.Min() <= 1);
        Assert.False(await db.PickupRequests.AnyAsync(p => p.AcceptedCollectorId == null));
        var events = await db.EmployeeDispatchEvents.CountAsync();
        await Planner.PrepareAsync(DateTime.UtcNow, default); await Dispatch.DispatchAsync(default);
        Assert.Equal(events, await db.EmployeeDispatchEvents.CountAsync());
    }
    [Fact]
    public async Task New_order_stays_with_region_employee_without_splitting()
    {
        await AddOrders(14); await Planner.PrepareAsync(DateTime.UtcNow, default); await Dispatch.DispatchAsync(default);
        await AddOrders(1); await Planner.PrepareAsync(DateTime.UtcNow, default); await Dispatch.DispatchAsync(default);
        var loads = await db.PickupRequests.GroupBy(p => p.AcceptedCollectorId).Select(g => g.Count()).ToListAsync();
        Assert.Single(loads); Assert.Equal(15, loads[0]);
    }
    [Fact]
    public async Task Future_orders_wait_and_catalog_covers_remote_points_without_radius_gaps()
    {
        await AddOrders(2, future: true); await Planner.PrepareAsync(DateTime.UtcNow, default);
        Assert.Equal(15, await db.EmployeeCollectionAreas.CountAsync()); Assert.Empty(await db.EmployeeDispatchShifts.ToListAsync());
        await AddOrders(1, remote: true); await Planner.PrepareAsync(DateTime.UtcNow, default); await Dispatch.DispatchAsync(default);
        Assert.Equal(1, await db.PickupRequests.CountAsync(p => p.Status == "SCHEDULED"));
        Assert.Equal(2, await db.PickupRequests.CountAsync(p => p.Status == "PENDING"));
    }
    [Fact]
    public async Task Stopped_employees_are_not_reactivated_and_other_depot_cannot_take_backlog()
    {
        var foreign = new Depot { Owner = User("DEPOT_OWNER"), VerificationStatus = "APPROVED" };
        db.DepotStaffs.Add(new() { Depot = foreign, User = User("DEPOT_EMPLOYEE"), StaffType = "DEPOT_EMPLOYEE" });
        await AddOrders(43); await Planner.PrepareAsync(DateTime.UtcNow, default); await Dispatch.DispatchAsync(default);
        Assert.False(await db.PickupRequests.AnyAsync(p => p.AcceptedCollectorId == null));
        for (var i = 0; i < 3; i++)
        {
            var shift = await db.EmployeeDispatchShifts.AsNoTracking().SingleAsync(s => s.Availability == "AVAILABLE");
            await Dispatch.ChangeAvailabilityAsync(shift.EmployeeId, shift.Id, new("STOPPED", "Nhân viên dừng thu gom để kiểm thử"), default);
            await Planner.PrepareAsync(DateTime.UtcNow, default); await Dispatch.DispatchAsync(default);
            Assert.Equal(i + 1, await db.EmployeeDispatchShifts.CountAsync(s => s.Availability == "STOPPED"));
            if (i < 2) Assert.Equal(43, await db.PickupRequests.CountAsync(p => p.Status == "SCHEDULED" && p.AcceptedCollectorId != shift.EmployeeId));
        }
        Assert.False(await db.EmployeeDispatchShifts.AnyAsync(s => db.EmployeeCollectionAreas.Any(a => a.Id == s.AreaId && a.DepotId == foreign.Id)));
        Assert.Equal(3, await db.EmployeeDispatchShifts.CountAsync());
        Assert.All(await db.EmployeeDispatchShifts.ToListAsync(), s => Assert.Equal("STOPPED", s.Availability));
        Assert.Equal(43, await db.PickupRequests.CountAsync(p => p.Status == "PENDING"));
    }
}
