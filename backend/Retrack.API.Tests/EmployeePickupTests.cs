using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Controllers.Employee;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public sealed class EmployeePickupTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly User employee = NewUser("DEPOT_EMPLOYEE");
    private readonly User colleague = NewUser("DEPOT_EMPLOYEE");
    private readonly User seller = NewUser("SELLER");
    private readonly Depot depot = new() { Name = "Kho A", Owner = NewUser("DEPOT_OWNER") };
    private readonly Depot otherDepot = new() { Name = "Kho B", Owner = NewUser("DEPOT_OWNER") };
    private static User NewUser(string role) => new() { Email = $"{Guid.NewGuid():N}@test.local", Role = role, FullName = role };

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Depots.AddRange(depot, otherDepot);
        db.Users.AddRange(employee, colleague, seller);
        db.DepotStaffs.AddRange(
            new DepotStaff { User = employee, Depot = depot, StaffType = "DEPOT_EMPLOYEE" },
            new DepotStaff { User = colleague, Depot = depot, StaffType = "DEPOT_EMPLOYEE" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private EmployeePickupController Controller(User? user = null, INotificationService? notifications = null) => new(db, notifications ?? new NotificationService(db))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, (user ?? employee).Id.ToString()), new Claim(ClaimTypes.Role, (user ?? employee).Role) }, "test")) } }
    };
    private PickupRequest Add(string status = "PENDING", User? collector = null, Depot? target = null, DateTime? preferred = null)
    {
        var pickup = new PickupRequest { Seller = seller, TargetDepot = target ?? depot, AcceptedCollector = collector,
            Status = status, Address = "123 Lê Lợi", Latitude = 10.77m, Longitude = 106.7m, PreferredDatetime = preferred };
        db.PickupRequests.Add(pickup);
        return pickup;
    }
    private static T Data<T>(IActionResult result) => Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value).Data!;

    [Fact]
    public async Task PoolFiltersByDepotAvailabilityAndSortsNullDatesLast()
    {
        var unscheduled = Add();
        var later = Add(preferred: DateTime.UtcNow.AddDays(2));
        var earlier = Add(preferred: DateTime.UtcNow.AddDays(1));
        Add(target: otherDepot); Add("SCHEDULED", colleague); Add(collector: colleague);
        await db.SaveChangesAsync();
        var result = Data<List<PickupPoolItemDto>>(await Controller().Pool(default));
        Assert.Equal(new[] { earlier.Id, later.Id, unscheduled.Id }, result.Select(p => p.Id));
        Assert.All(result, p => Assert.Equal(seller.FullName, p.SellerName));
    }

    [Fact]
    public async Task DashboardUsesOwnOrdersAndUtcDayBoundaries()
    {
        Add(); Add(target: otherDepot);
        var active = Add("SCHEDULED", employee);
        var midnight = Add("DONE", employee); midnight.UpdatedAt = DateTime.UtcNow.Date;
        var yesterday = Add("DONE", employee); yesterday.UpdatedAt = DateTime.UtcNow.Date.AddTicks(-1);
        var tomorrow = Add("DONE", employee); tomorrow.UpdatedAt = DateTime.UtcNow.Date.AddDays(1);
        Add("DONE", colleague); Add("DONE", employee, otherDepot);
        await db.SaveChangesAsync();
        var result = Data<EmployeeDashboardDto>(await Controller().Dashboard(default));
        Assert.Equal(1, result.AvailableCount); Assert.Equal(active.Id, result.ActivePickup?.Id);
        Assert.Equal(2, result.CompletedToday); Assert.Equal(4, result.TotalPickupsCompleted);
    }

    [Fact]
    public async Task AcceptancePersistsOwnershipAndExactlyOneSellerNotification()
    {
        var pickup = Add(); await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await Controller().Accept(pickup.Id, default));
        Assert.IsType<ConflictObjectResult>(await Controller(colleague).Accept(pickup.Id, default));
        Assert.IsType<ConflictObjectResult>(await Controller().Accept(pickup.Id, default));
        await db.Entry(pickup).ReloadAsync();
        Assert.Equal("SCHEDULED", pickup.Status); Assert.Equal(employee.Id, pickup.AcceptedCollectorId);
        var notification = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(seller.Id, notification.UserId);
        Assert.Empty(Data<List<PickupPoolItemDto>>(await Controller().Pool(default)));
        Assert.True(Data<EmployeePickupDetailDto>(await Controller().Detail(pickup.Id, default)).IsAcceptedByMe);
        Assert.IsType<NotFoundObjectResult>(await Controller(colleague).Detail(pickup.Id, default));
    }

    [Fact]
    public async Task OtherDepotAndUnknownOrdersCannotBeReadOrAccepted()
    {
        var pickup = Add(target: otherDepot); await db.SaveChangesAsync();
        Assert.IsType<NotFoundObjectResult>(await Controller().Detail(pickup.Id, default));
        Assert.IsType<NotFoundObjectResult>(await Controller().Accept(pickup.Id, default));
        Assert.IsType<NotFoundObjectResult>(await Controller().Accept(Guid.NewGuid(), default));
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Theory]
    [InlineData("DONE")]
    [InlineData("SCHEDULED")]
    [InlineData("WEIGHED")]
    public async Task NonPendingOrdersAreNeverReopened(string status)
    {
        var pickup = Add(status); await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await Controller().Accept(pickup.Id, default));
        await db.Entry(pickup).ReloadAsync(); Assert.Equal(status, pickup.Status);
    }

    [Fact]
    public async Task InactiveMembershipAndInactiveUserCannotAccessEndpoints()
    {
        var staff = await db.DepotStaffs.SingleAsync(s => s.UserId == employee.Id);
        staff.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller().Pool(default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller().Accept(Guid.NewGuid(), default));
        staff.IsActive = true; employee.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller().Dashboard(default));
        employee.IsActive = true; employee.Role = "DRIVER"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller().Pool(default));
    }

    [Fact]
    public async Task NotificationFailureRollsBackAcceptance()
    {
        var pickup = Add(); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<IOException>(() => Controller(notifications: new FailingNotification()).Accept(pickup.Id, default));
        await db.Entry(pickup).ReloadAsync();
        Assert.Equal("PENDING", pickup.Status); Assert.Null(pickup.AcceptedCollectorId);
        Assert.Empty(await db.Notifications.ToListAsync());
    }
    private sealed class FailingNotification : INotificationService
    {
        public Task SendAsync(Guid userId, string title, string message) => throw new IOException("Simulated notification failure");
        public Task<IEnumerable<object>> GetByUserIdAsync(Guid userId) => throw new NotSupportedException();
        public Task MarkAsReadAsync(Guid id) => throw new NotSupportedException();
    }
}
