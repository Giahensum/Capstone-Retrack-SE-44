using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Xunit;
namespace Retrack.Tests;

public sealed class DepotOperationsTests : IAsyncLifetime
{
    private readonly Guid owner = Guid.NewGuid(), otherOwner = Guid.NewGuid(), employee = Guid.NewGuid(), driver = Guid.NewGuid(), seller = Guid.NewGuid(), depot = Guid.NewGuid(), secondDepot = Guid.NewGuid(), foreignDepot = Guid.NewGuid(), batch = Guid.NewGuid(), evt = Guid.NewGuid();
    private readonly List<Guid> users = [];
    private static AppDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")!;
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test") throw new InvalidOperationException("Sai DB test");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
    private static DepotService Scope(AppDbContext db) => new(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
    private static DepotOperationsService Service(AppDbContext db) => new(new DepotOperationsRepository(db), Scope(db), new DepotBatchRepository(db), new DepotUnitOfWork(db));
    private static DepotReportService Reports(AppDbContext db) => new(new DepotUnitOfWork(db), new DepotReportRepository(db), new DepotStaffRepository(db), Scope(db), new InventoryService(new DepotInventoryRepository(db), Scope(db)));
    public async Task InitializeAsync()
    {
        await using var db = Open(); await TestDatabase.MigrateAsync(db);
        foreach (var (id, role) in new[] { (owner,"DEPOT_OWNER"), (otherOwner,"DEPOT_OWNER"), (employee,"DEPOT_EMPLOYEE"), (driver,"DRIVER"), (seller,"SELLER") })
        { users.Add(id); db.Users.Add(new User { Id = id, Role = role, FullName = role, Email = $"ops-{id}@test.invalid" }); }
        db.Depots.AddRange(new Depot { Id = depot, OwnerId = owner, Name = "Kho ops", VerificationStatus = "APPROVED" }, new Depot { Id = secondDepot, OwnerId = owner, Name = "Kho thứ hai" }, new Depot { Id = foreignDepot, OwnerId = otherOwner, Name = "Kho người khác" });
        db.DepotStaffs.AddRange(new DepotStaff { DepotId = depot, UserId = employee, StaffType = "DEPOT_EMPLOYEE" }, new DepotStaff { DepotId = depot, UserId = driver, StaffType = "DRIVER" });
        var lot = new InventoryBatch { Id = batch, DepotId = depot, MaterialType = "IRON", DeclaredWeightKg = 10, Status = "IN_TRANSIT" };
        var job = new TransportJob { Batch = lot, DriverId = driver, Status = "IN_TRANSIT" };
        db.DriverDeliveryEvents.Add(new DriverDeliveryEvent { Id = evt, Job = job, DriverId = driver, OperationId = Guid.NewGuid(), Action = "incident", Reason = "Xe cần hỗ trợ" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open();
        await db.Notifications.Where(x => users.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.DriverDeliveryEvents.Where(x => x.Job.Batch.DepotId == depot).ExecuteDeleteAsync();
        await db.TransportJobs.Where(x => x.Batch.DepotId == depot).ExecuteDeleteAsync();
        await db.InventoryBatches.Where(x => x.DepotId == depot).ExecuteDeleteAsync();
        await db.PickupRequests.Where(x => x.TargetDepotId == depot || x.TargetDepotId == secondDepot).ExecuteDeleteAsync();
        await db.DepotStaffs.Where(x => x.DepotId == depot).ExecuteDeleteAsync();
        await db.Depots.Where(x => x.Id == depot || x.Id == secondDepot || x.Id == foreignDepot).ExecuteDeleteAsync();
        await db.Users.Where(x => users.Contains(x.Id)).ExecuteDeleteAsync();
    }
    [Fact]
    public async Task Notifications_scope_by_recipient_and_selected_warehouse_and_read_is_idempotent()
    {
        await using var db = Open(); var job = await db.TransportJobs.SingleAsync(x => x.BatchId == batch);
        var visible = new Notification { UserId = owner, TransportJobId = job.Id, Title = "Sự cố" };
        var otherPickup = new PickupRequest { SellerId = seller, TargetDepotId = secondDepot, Address = "Đà Nẵng" };
        db.PickupRequests.Add(otherPickup);
        db.Notifications.AddRange(visible, new Notification { UserId = owner, PickupRequestId = otherPickup.Id, Title = "Kho khác" }, new Notification { UserId = otherOwner, Title = "Riêng người khác" });
        await db.SaveChangesAsync();
        var rows = await Service(db).NoticesAsync(owner, depot, new());
        Assert.Equal(visible.Id, Assert.Single(rows.Items).Id); Assert.Equal(batch, rows.Items[0].BatchId);
        Assert.Equal(1, await Service(db).UnreadAsync(owner, depot));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).ReadAsync(owner, secondDepot, visible.Id));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).NoticesAsync(otherOwner, depot, new()));
        await Service(db).ReadAsync(owner, depot, visible.Id); await Service(db).ReadAsync(owner, depot, visible.Id);
        Assert.Equal(0, await Service(db).UnreadAsync(owner, depot));
        Assert.Empty((await Service(db).NoticesAsync(owner, depot, new() { Status = "UNREAD" })).Items);
    }
    [Fact]
    public async Task Incident_followup_preserves_transport_stock_and_history_and_rejects_foreign_owners()
    {
        await using var db = Open(); var dto = new Retrack.API.DTOs.Depot.CreateIncidentFollowupDto(Guid.NewGuid(), "CONTACTING", "Đã liên hệ tài xế và nhà máy");
        await Service(db).FollowupAsync(owner, depot, evt, dto); await Service(db).FollowupAsync(owner, depot, evt, dto);
        await Service(db).FollowupAsync(owner, depot, evt, new(Guid.NewGuid(), "CLOSED", "Đã khép theo dõi nội bộ"));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).FollowupAsync(otherOwner, depot, evt, dto));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).FollowupAsync(owner, secondDepot, evt, dto));
        Assert.Equal(2, (await Service(db).FollowupsAsync(owner, depot, evt, new())).TotalCount);
        Assert.Equal("CLOSED", (await Service(db).EventsAsync(owner, depot, batch, new())).Items.Single().FollowupStatus);
        db.ChangeTracker.Clear();
        Assert.Equal("IN_TRANSIT", (await db.InventoryBatches.SingleAsync(x => x.Id == batch)).Status);
        Assert.Equal("IN_TRANSIT", (await db.TransportJobs.SingleAsync(x => x.BatchId == batch)).Status);
        Assert.False(await db.DepotStockReceipts.AnyAsync(x => x.DepotId == depot));
    }
    [Fact]
    public async Task Staff_summary_and_top_five_cover_the_entire_period_not_only_current_page()
    {
        await using var db = Open(); var lastStaff = Guid.Empty;
        for (var n = 1; n <= 21; n++)
        {
            var user = new User { Role = "DEPOT_EMPLOYEE", Email = $"summary-{Guid.NewGuid()}@test.invalid", FullName = $"Nhân viên {n}" }; users.Add(user.Id); db.Users.Add(user);
            var staff = new DepotStaff { DepotId = depot, UserId = user.Id, StaffType = "DEPOT_EMPLOYEE" }; db.DepotStaffs.Add(staff); lastStaff = staff.Id;
            db.PickupRequests.Add(new PickupRequest { SellerId = seller, AcceptedCollectorId = user.Id, TargetDepotId = depot, Status = "DONE", Address = "Đà Nẵng", GrossAmount = n * 1000,
                Items = [new() { MaterialType = "IRON", WeightKg = n, PricePerKg = 1000, SubTotal = n * 1000 }] });
        }
        await db.SaveChangesAsync();
        var summary = await Reports(db).PerformanceSummaryAsync(owner, depot, new());
        Assert.Equal(23, summary.StaffCount); Assert.Equal(21, summary.CompletedCount); Assert.Equal(231, summary.WeightKg); Assert.Equal(231000, summary.PurchaseAmount);
        Assert.Equal(new decimal[] {21,20,19,18,17}, summary.TopStaff.Select(x => x.WeightKg));
        Assert.Single((await Reports(db).PerformanceAsync(owner, depot, new(), new() { PageSize = 1 })).Items);
        Assert.Equal(21, (await Reports(db).HistoryAsync(owner, depot, lastStaff, new(), new())).Items.Single().WeightKg);
        Assert.Equal(0, (await Reports(db).PerformanceSummaryAsync(owner, depot, new() { From = new(2020,1,1), To = new(2020,1,31) })).CompletedCount);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Reports(db).PerformanceSummaryAsync(otherOwner, depot, new()));
    }
}
