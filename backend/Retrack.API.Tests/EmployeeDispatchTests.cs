using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.API.Tests;

public sealed class EmployeeDispatchTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly User seller = NewUser("SELLER"), first = NewUser("DEPOT_EMPLOYEE"), second = NewUser("DEPOT_EMPLOYEE");
    private readonly Depot depot = new() { Owner = NewUser("DEPOT_OWNER"), VerificationStatus = "APPROVED" };
    private EmployeeCollectionArea area = null!, nearby = null!;
    private EmployeeDispatchShift a = null!, b = null!;
    private EmployeeDispatchService Service => new(db);
    private static User NewUser(string role) => new() { Email = $"{Guid.NewGuid():N}@test.local", Role = role, FullName = role };
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(seller);
        db.DepotStaffs.AddRange(new DepotStaff { User = first, Depot = depot, StaffType = first.Role },
            new DepotStaff { User = second, Depot = depot, StaffType = second.Role });
        area = new() { DepotId = depot.Id, Name = "Khu A", Latitude = 16.05, Longitude = 108.22 };
        nearby = new() { DepotId = depot.Id, Name = "Khu B", Latitude = 16.06, Longitude = 108.22 };
        db.EmployeeCollectionAreas.AddRange(area, nearby);
        a = Shift(first, area); b = Shift(second, nearby);
        db.EmployeeDispatchShifts.AddRange(a,b);
        await db.SaveChangesAsync();
    }
    private static EmployeeDispatchShift Shift(User user, EmployeeCollectionArea area) => new()
    {
        EmployeeId = user.Id, AreaId = area.Id, StartsAt = DateTime.UtcNow.AddMinutes(-1),
        EndsAt = DateTime.UtcNow.AddHours(10), MaxPendingOrders = 10
    };
    private async Task<Guid> Pickup(string status = "PENDING", Guid? employee = null, DateTime? preferred = null)
    {
        var p = new PickupRequest { SellerId = seller.Id, TargetDepotId = depot.Id, Latitude = 16.05m,
            Longitude = 108.22m, Address = "Điểm kiểm thử", Status = status, AcceptedCollectorId = employee, PreferredDatetime = preferred };
        db.PickupRequests.Add(p); await db.SaveChangesAsync(); return p.Id;
    }
    private Task<PickupRequest> Read(Guid id) => db.PickupRequests.AsNoTracking().SingleAsync(p => p.Id == id);
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    [Fact]
    public async Task AssignsHomeAreaAndDoesNotDuplicateOnRetry()
    {
        var id = await Pickup(); await Service.DispatchAsync(default); await Service.DispatchAsync(default);
        Assert.Equal(first.Id, (await Read(id)).AcceptedCollectorId);
        Assert.Equal("SCHEDULED", (await Read(id)).Status);
        Assert.Single(await db.EmployeeDispatchEvents.ToListAsync());
        Assert.Equal(2, await db.Notifications.CountAsync());
    }
    [Fact]
    public async Task FullTransfersUnstartedButKeepsCargoAndHistory()
    {
        var id = await Pickup("SCHEDULED", first.Id);
        var started = await Pickup("IN_PROGRESS", first.Id);
        await Service.ChangeAvailabilityAsync(first.Id,a.Id,new("FULL","Xe đã đầy, về bàn giao hàng"),default);
        Assert.Equal(second.Id,(await Read(id)).AcceptedCollectorId);
        Assert.Equal(first.Id,(await Read(started)).AcceptedCollectorId);
        Assert.Equal(first.Id,(await db.EmployeeDispatchEvents.SingleAsync()).FromEmployeeId);
    }
    [Fact]
    public async Task AllUnavailableQueuesAndNotifiesOwnerOnce()
    {
        a.Availability = b.Availability = "STOPPED"; await db.SaveChangesAsync();
        var id = await Pickup(); await Service.DispatchAsync(default); await Service.DispatchAsync(default);
        Assert.Null((await Read(id)).AcceptedCollectorId);
        Assert.Equal("PENDING",(await Read(id)).Status);
        Assert.Single(await db.Notifications.ToListAsync());
        Assert.NotEqual("ASSIGNED",(await db.EmployeeDispatchQueues.SingleAsync()).Reason);
    }
    [Fact]
    public async Task RepeatedStopsDoNotCycleBackToPreviousEmployee()
    {
        var id = await Pickup(); await Service.DispatchAsync(default);
        await Service.ChangeAvailabilityAsync(first.Id,a.Id,new("FULL","Xe đã đầy cần bàn giao hàng"),default);
        await Service.ChangeAvailabilityAsync(first.Id,a.Id,new("AVAILABLE",null),default);
        await Service.ChangeAvailabilityAsync(second.Id,b.Id,new("STOPPED","Xe hỏng không thể tiếp tục ca"),default);
        await Service.DispatchAsync(default);
        Assert.Null((await Read(id)).AcceptedCollectorId);
        Assert.Equal(3,await db.EmployeeDispatchEvents.CountAsync());
    }
    [Fact]
    public async Task FutureDayWaitsAndFreshImmediateRequestIsAssigned()
    {
        var future = await Pickup(preferred:DateTime.UtcNow.AddDays(2));
        var immediate = await Pickup();
        await Service.DispatchAsync(default);
        Assert.Null((await Read(future)).AcceptedCollectorId);
        Assert.Equal(first.Id,(await Read(immediate)).AcceptedCollectorId);
    }
    [Fact]
    public async Task WorkloadLimitFallsBackToNeighbor()
    {
        a.MaxPendingOrders = 1; await db.SaveChangesAsync();
        await Pickup("IN_PROGRESS",first.Id);
        var id = await Pickup(); await Service.DispatchAsync(default);
        Assert.Equal(second.Id,(await Read(id)).AcceptedCollectorId);
    }
    [Fact]
    public async Task BatchTransferRespectsRecipientLimit()
    {
        b.MaxPendingOrders = 1; await db.SaveChangesAsync();
        await Pickup("SCHEDULED", first.Id); await Pickup("SCHEDULED", first.Id);
        await Service.ChangeAvailabilityAsync(first.Id,a.Id,new("FULL","Xe đầy cần về bàn giao"),default);
        Assert.Equal(1,await db.PickupRequests.CountAsync(p => p.AcceptedCollectorId == second.Id));
        Assert.Equal(1,await db.PickupRequests.CountAsync(p => p.Status == "PENDING"));
    }
    [Fact]
    public async Task ExpiredShiftTransfersOrder()
    {
        a.EndsAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        var id = await Pickup("SCHEDULED",first.Id); await Service.DispatchAsync(default);
        Assert.Equal(second.Id,(await Read(id)).AcceptedCollectorId);
    }
    [Theory]
    [InlineData("WEIGHED")][InlineData("SELLER_CONFIRMED")][InlineData("DONE")][InlineData("CANCELLED")]
    public async Task LaterStatesNeverReassigned(string state)
    {
        var id = await Pickup(state,first.Id);
        await Service.ChangeAvailabilityAsync(first.Id,a.Id,new("STOPPED","Ngừng ca vì việc cá nhân"),default);
        await Service.DispatchAsync(default);
        Assert.Equal(first.Id,(await Read(id)).AcceptedCollectorId);
        Assert.Equal(state,(await Read(id)).Status);
    }
    [Fact]
    public async Task OtherAccountAndInvalidReasonCannotChangeShift()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.ChangeAvailabilityAsync(second.Id,a.Id,new("FULL","Xe đã đầy cần về kho"),default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.ChangeAvailabilityAsync(first.Id,a.Id,new("FULL",""),default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.ChangeAvailabilityAsync(first.Id,a.Id,new("INVALID",""),default));
    }
    [Fact]
    public async Task StoppedShiftCannotSelfResume()
    {
        await Service.ChangeAvailabilityAsync(first.Id,a.Id,new("STOPPED","Ngừng ca vì việc cá nhân"),default);
        await Assert.ThrowsAsync<DepotConflictException>(() => Service.ChangeAvailabilityAsync(first.Id,a.Id,new("AVAILABLE",null),default));
    }
    [Fact]
    public async Task UnverifiedDepotDoesNotReceiveNewOrders()
    {
        depot.VerificationStatus = "UNVERIFIED"; await db.SaveChangesAsync();
        var id = await Pickup(); await Service.DispatchAsync(default);
        Assert.Null((await Read(id)).AcceptedCollectorId);
    }
}
