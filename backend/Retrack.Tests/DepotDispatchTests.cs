using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Xunit;
namespace Retrack.Tests;

public sealed class DepotDispatchTests : IAsyncLifetime
{
    private readonly Guid owner = Guid.NewGuid(), other = Guid.NewGuid(), seller = Guid.NewGuid(), employee = Guid.NewGuid(), depot = Guid.NewGuid(), foreign = Guid.NewGuid(), area = Guid.NewGuid(), pickup = Guid.NewGuid();
    private static AppDbContext Open()
    {
        var cs = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")!;
        if (new NpgsqlConnectionStringBuilder(cs).Database != "Retrack_TV2_test" || cs != Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")) throw new InvalidOperationException("Sai DB test");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
    }
    private static DepotDispatchService Service(AppDbContext db) => new(new DepotDispatchRepository(db), new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db)), new DepotUnitOfWork(db));
    public async Task InitializeAsync()
    {
        await using var db = Open(); await TestDatabase.MigrateAsync(db);
        foreach (var (id, role) in new[] { (owner, "DEPOT_OWNER"), (other, "DEPOT_OWNER"), (seller, "SELLER"), (employee, "DEPOT_EMPLOYEE") }) db.Users.Add(new() { Id = id, Role = role, FullName = role, Email = $"dispatch-owner-{id}@test.invalid" });
        db.Depots.AddRange(new Depot { Id = depot, OwnerId = owner, Name = "Kho điều phối", VerificationStatus = "APPROVED" }, new Depot { Id = foreign, OwnerId = other, Name = "Kho khác" });
        db.DepotStaffs.Add(new() { DepotId = depot, UserId = employee, StaffType = "DEPOT_EMPLOYEE" });
        db.EmployeeCollectionAreas.Add(new() { Id = area, DepotId = depot, Name = "Ngũ Hành Sơn", Latitude = 16.02, Longitude = 108.25 });
        db.PickupRequests.Add(new() { Id = pickup, TargetDepotId = depot, SellerId = seller, Address = "Đà Nẵng", Status = "PENDING" }); await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open(); await db.Notifications.Where(x => x.DepotId == depot).ExecuteDeleteAsync();
        await db.EmployeeDispatchEvents.Where(x => db.PickupRequests.Any(p => p.Id == x.PickupId && p.TargetDepotId == depot)).ExecuteDeleteAsync(); await db.PickupRequests.Where(x => x.TargetDepotId == depot).ExecuteDeleteAsync();
        await db.EmployeeDispatchShifts.Where(x => x.EmployeeId == employee).ExecuteDeleteAsync(); await db.EmployeeCollectionAreas.Where(x => x.DepotId == depot).ExecuteDeleteAsync();
        await db.DepotStaffs.Where(x => x.DepotId == depot).ExecuteDeleteAsync(); await db.Depots.Where(x => x.Id == depot || x.Id == foreign).ExecuteDeleteAsync(); await db.Users.Where(x => x.Id == owner || x.Id == other || x.Id == seller || x.Id == employee).ExecuteDeleteAsync();
    }
    private DispatchShiftInput Input() => new(area, employee, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()), DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds()), "motorcycle", 100, 10, Guid.NewGuid());
    [Fact]
    public async Task Setup_and_lists_are_scoped_paginated_and_shift_creation_is_retry_safe()
    {
        await using var db = Open(); var service = Service(db);
        Assert.Single((await service.AreasAsync(owner, depot, new() { PageSize = 1 })).Items);
        Assert.Equal(employee,Assert.Single((await service.EmployeesAsync(owner,depot,new(){PageSize=1})).Items).UserId);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.AreasAsync(other, depot, new()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AreasAsync(owner, depot, new() { PageSize = 101 }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAreaAsync(owner, depot, Guid.NewGuid(), new("Lỗi", 90.001, 108, 3000, true)));
        var input = Input();
        var shift = await service.CreateShiftAsync(owner, depot, input); Assert.Equal(shift.Id, (await service.CreateShiftAsync(owner, depot, input)).Id);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.CreateShiftAsync(owner, depot, input with { OperationId = Guid.NewGuid() }));
        Assert.Single((await service.ShiftsAsync(owner, depot, new() { Status = "CURRENT" })).Items); Assert.Single((await service.ShiftsAsync(owner, depot, new(), true)).Items);
        Assert.Single((await service.PickupsAsync(owner, depot, new())).Items); Assert.Equal(1, (await service.SummaryAsync(owner, depot)).WaitingCount);
    }
    [Fact]
    public async Task Assignment_retries_preserve_state_and_publish_one_set_of_notices()
    {
        await using var db = Open(); var service = Service(db); var shift = await service.CreateShiftAsync(owner, depot, Input()); var dto = new DispatchAssignInput(Guid.NewGuid(), shift.Id, "Chủ kho liên hệ và phân công xử lý");
        await service.AssignAsync(owner, depot, pickup, dto); await service.AssignAsync(owner, depot, pickup, dto);
        Assert.Equal("SCHEDULED", (await service.DetailAsync(owner, depot, pickup)).Status); Assert.Equal(employee, (await service.DetailAsync(owner, depot, pickup)).EmployeeId);
        Assert.Single((await service.HistoryAsync(owner, depot, pickup, new())).Items); Assert.Equal(2, await db.Notifications.CountAsync(x => x.DepotId == depot));
        Assert.Equal(0, (await service.SummaryAsync(owner, depot)).WaitingCount); Assert.Single((await service.PickupsAsync(owner, depot, new() { Status = "SCHEDULED" })).Items);
        Assert.Empty((await service.ShiftsAsync(owner, depot, new(), true, employee)).Items);
        await new Retrack.API.Services.Employee.EmployeeDispatchService(db).DispatchAsync(default);
        Assert.Equal(employee, (await service.DetailAsync(owner, depot, pickup)).EmployeeId);
        Assert.Equal(pickup, Assert.Single(await service.LegacyHistoryAsync(owner, depot, pickup, 1)).PickupId);
        using var configuration = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(await service.ConfigurationAsync(owner, depot, 1), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        Assert.Equal(depot, configuration.RootElement.GetProperty("areas")[0].GetProperty("depotId").GetGuid());
        Assert.Equal(0, configuration.RootElement.GetProperty("waitingCount").GetInt32());
        Assert.Equal(shift.Id, configuration.RootElement.GetProperty("shifts")[0].GetProperty("id").GetGuid());
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.AssignAsync(other, depot, pickup, dto));
        await Assert.ThrowsAsync<DepotConflictException>(() => service.AssignAsync(owner, depot, pickup, dto with { Reason = "Nội dung khác không được ghi đè" }));
    }
    [Theory]
    [InlineData("DONE")]
    [InlineData("IN_PROGRESS")]
    [InlineData("AWAITING_PAYMENT")]
    public async Task Cannot_assign_cargo_or_terminal_states(string state)
    {
        await using var db = Open(); var row = await db.PickupRequests.SingleAsync(x => x.Id == pickup); row.Status = state; await db.SaveChangesAsync(); var shift = await Service(db).CreateShiftAsync(owner, depot, Input());
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).AssignAsync(owner, depot, pickup, new(Guid.NewGuid(), shift.Id, "Không được thay đổi người đang giữ hàng")));
        Assert.Empty(await db.EmployeeDispatchEvents.Where(x => x.PickupId == pickup).ToListAsync());
    }
    [Fact]
    public async Task Checkin_future_dates_unverified_and_full_shift_block_assignment()
    {
        await using var db = Open(); var service = Service(db); var shift = await service.CreateShiftAsync(owner, depot, Input()); var dto = new DispatchAssignInput(Guid.NewGuid(), shift.Id, "Chủ kho xử lý đơn chờ phân công");
        var row = await db.PickupRequests.SingleAsync(x => x.Id == pickup); row.CheckinImageUrl = "https://example.test/checkin"; await db.SaveChangesAsync(); await Assert.ThrowsAsync<DepotConflictException>(() => service.AssignAsync(owner, depot, pickup, dto));
        row.CheckinImageUrl = null; row.PreferredDatetime = DateTime.UtcNow.AddDays(1); await db.SaveChangesAsync(); await Assert.ThrowsAsync<DepotConflictException>(() => service.AssignAsync(owner, depot, pickup, dto));
        row.PreferredDatetime = null; shift.Availability = "FULL"; shift.Reason = "Xe đã đầy hàng, chờ bàn giao"; await db.SaveChangesAsync(); await Assert.ThrowsAsync<DepotConflictException>(() => service.AssignAsync(owner, depot, pickup, dto));
        shift.Availability = "AVAILABLE"; shift.Reason = null; var warehouse = await db.Depots.SingleAsync(x => x.Id == depot); warehouse.VerificationStatus = "UNVERIFIED"; await db.SaveChangesAsync(); await Assert.ThrowsAsync<DepotConflictException>(() => service.AssignAsync(owner, depot, pickup, dto));
        Assert.Empty(await db.Notifications.Where(x => x.DepotId == depot).ToListAsync());
    }
    [Fact]
    public async Task Concurrent_assignments_have_one_effect_and_wrong_warehouse_shift_is_denied()
    {
        Guid shiftId; await using (var db = Open()) shiftId = (await Service(db).CreateShiftAsync(owner, depot, Input())).Id;
        var dto = new DispatchAssignInput(Guid.NewGuid(), shiftId, "Phân công đồng thời kiểm thử nguyên tử");
        async Task Assign() { await using var db = Open(); await Service(db).AssignAsync(owner, depot, pickup, dto); }
        await Task.WhenAll(Assign(), Assign());
        await using var check = Open(); Assert.Equal(1, await check.EmployeeDispatchEvents.CountAsync(x => x.PickupId == pickup)); Assert.Equal(2, await check.Notifications.CountAsync(x => x.DepotId == depot));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(check).DetailAsync(owner, foreign, pickup));
    }
    [Fact]
    public async Task Owner_stops_shift_once_and_worker_preserves_cargo_while_unstarted_order_returns_to_queue()
    {
        await using var db = Open(); var service = Service(db); var shift = await service.CreateShiftAsync(owner, depot, Input());
        await service.AssignAsync(owner, depot, pickup, new(Guid.NewGuid(), shift.Id, "Phân công trước khi kiểm tra ngừng ca"));
        await service.StopShiftAsync(owner, depot, shift.Id, "Nhân viên xin nghỉ có thông báo chủ kho"); await service.StopShiftAsync(owner, depot, shift.Id, "Nhân viên xin nghỉ có thông báo chủ kho");
        Assert.Equal("STOPPED", (await service.ShiftsAsync(owner, depot, new())).Items.Single().Availability);
        Assert.Empty((await service.ShiftsAsync(owner, depot, new(), true)).Items);
        // Owner không trực tiếp sửa giao nhận/tồn khi ngừng ca; worker TV3 xử lý đơn chưa check-in.
        Assert.Equal("SCHEDULED", (await service.DetailAsync(owner, depot, pickup)).Status);
        await new Retrack.API.Services.Employee.EmployeeDispatchService(db).DispatchAsync(default);
        Assert.Equal("PENDING", (await service.DetailAsync(owner, depot, pickup)).Status);
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.Title == "Chủ kho đã ngừng ca thu gom" && x.DepotId == depot));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.StopShiftAsync(other, depot, shift.Id, "Không đúng chủ kho của nhân viên"));
    }
    [Fact]
    public async Task Concurrent_different_orders_cannot_exceed_shift_order_limit()
    {
        Guid shiftId; var second = Guid.NewGuid();
        await using (var db = Open())
        {
            shiftId = (await Service(db).CreateShiftAsync(owner, depot, Input() with { MaxPendingOrders = 1 })).Id;
            db.PickupRequests.Add(new() { Id = second, TargetDepotId = depot, SellerId = seller, Address = "Đơn thứ hai kiểm thử giới hạn", Status = "PENDING" }); await db.SaveChangesAsync();
        }
        async Task<bool> Assign(Guid id)
        {
            await using var db = Open();
            try { await Service(db).AssignAsync(owner, depot, id, new(Guid.NewGuid(), shiftId, "Kiểm thử hai đơn tranh suất cuối ca")); return true; }
            catch (DepotConflictException) { return false; }
        }
        var results = await Task.WhenAll(Assign(pickup), Assign(second)); Assert.Single(results, x => x);
        await using var check = Open(); Assert.Equal(1, await check.PickupRequests.CountAsync(x => x.TargetDepotId == depot && x.Status == "SCHEDULED"));
        Assert.Empty((await Service(check).ShiftsAsync(owner, depot, new(), true)).Items);
        Assert.Equal(2, await check.Notifications.CountAsync(x => x.DepotId == depot));
    }
}
