using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shared;
using Xunit;
namespace Retrack.Tests;

public sealed class DepotCapacityTests : IAsyncLifetime
{
    private readonly Guid owner = Guid.NewGuid(), depot = Guid.NewGuid(), admin = Guid.NewGuid();
    private readonly string suffix = Guid.NewGuid().ToString("N");
    private static AppDbContext Open()
    {
        var cs = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")!;
        if (new NpgsqlConnectionStringBuilder(cs).Database != "Retrack_TV2_test") throw new InvalidOperationException("Sai DB test");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
    }
    private StaffService Service(AppDbContext db) => new(new DepotStaffRepository(db), new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db)));
    private CreateStaffDto Input(string tag = "new") => new() { FullName = "Nhân sự thử nghiệm", Email = tag + suffix + "@test.invalid", Phone = "0901234567", Password = "TestOnly123!", Role = "DRIVER" };
    private async Task<DepotStaff> Add(AppDbContext db, int index, bool membership = true, bool account = true)
    {
        var role = index % 2 == 0 ? "DRIVER" : "DEPOT_EMPLOYEE";
        var s = new DepotStaff { DepotId = depot, StaffType = role, IsActive = membership,
            User = new User { Email = index + suffix + "@test.invalid", FullName = "Nhân sự " + index, Role = role, IsActive = account } };
        db.DepotStaffs.Add(s); await db.SaveChangesAsync(); return s;
    }
    public async Task InitializeAsync()
    {
        await using var db = Open(); await TestDatabase.MigrateAsync(db);
        db.Users.AddRange(new User { Id = owner, Email = "owner" + suffix + "@test.invalid", FullName = "Chủ kho", Role = "DEPOT_OWNER" },
            new User { Id = admin, Email = "admin" + suffix + "@test.invalid", FullName = "Admin", Role = "ADMIN" });
        db.Depots.Add(new Retrack.API.Models.Depot { Id = depot, OwnerId = owner, Name = "Kho giới hạn", Address = "Đà Nẵng" }); await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open(); await db.AuditLogs.Where(x => x.UserId == admin).ExecuteDeleteAsync();
        await db.DepotStaffs.Where(x => x.DepotId == depot).ExecuteDeleteAsync(); await db.Depots.Where(x => x.Id == depot).ExecuteDeleteAsync();
        await db.Users.Where(x => x.Email.EndsWith(suffix + "@test.invalid")).ExecuteDeleteAsync();
    }
    [Fact]
    public async Task Counts_collectors_and_drivers_together_but_excludes_disabled_accounts_and_memberships()
    {
        await using var db = Open(); for (var i = 0; i < 9; i++) await Add(db, i);
        await Add(db, 11, false); await Add(db, 12, true, false);
        await Service(db).CreateAsync(owner, depot, Input());
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).CreateAsync(owner, depot, Input("overflow")));
        Assert.False(await db.Users.AnyAsync(x => x.Email == Input("overflow").Email));
    }
    [Fact]
    public async Task Reactivation_obeys_limit_existing_staff_can_be_edited_and_disabling_releases_slot()
    {
        await using var db = Open(); DepotStaff? first = null;
        for (var i = 0; i < 10; i++) { var s = await Add(db, i); first ??= s; }
        var inactive = await Add(db, 11, false, false);
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).UpdateAsync(owner, depot, inactive.Id, new() { FullName = "Mới", Phone = "0901234567", IsActive = true }));
        await Service(db).UpdateAsync(owner, depot, first!.Id, new() { FullName = "Đổi tên tại hạn mức", Phone = "0901234567", IsActive = true });
        await Service(db).UpdateAsync(owner, depot, first.Id, new() { FullName = first.User.FullName, Phone = "0901234567", IsActive = false });
        Assert.True((await Service(db).UpdateAsync(owner, depot, inactive.Id, new() { FullName = "Kích hoạt lại", Phone = "0901234567", IsActive = true })).IsActive);
        Assert.Equal(10, await db.DepotStaffs.CountAsync(x => x.DepotId == depot && x.IsActive && x.User.IsActive));
    }
    [Fact]
    public async Task Concurrent_creation_and_reactivation_share_the_last_slot()
    {
        Guid inactive;
        await using (var db = Open()) { for (var i = 0; i < 9; i++) await Add(db, i); inactive = (await Add(db, 11, false, false)).Id; }
        async Task<bool> Write(bool activate)
        {
            await using var db = Open();
            try { if (activate) await Service(db).UpdateAsync(owner, depot, inactive, new() { FullName = "Kích hoạt", Phone = "0901234567", IsActive = true }); else await Service(db).CreateAsync(owner, depot, Input()); return true; }
            catch (DepotConflictException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Write(false), Write(true)), x => x);
        await using var verify = Open(); Assert.Equal(10, await verify.DepotStaffs.CountAsync(x => x.DepotId == depot && x.IsActive && x.User.IsActive));
    }
    [Fact]
    public async Task Admin_cannot_reactivate_account_past_staff_limit()
    {
        await using var db = Open(); for (var i = 0; i < 10; i++) await Add(db, i);
        var blocked = await Add(db, 11, true, false);
        var service = new AdminService(new UserRepository(db), new MarketPriceRepository(db), new AuditLogRepository(db), new PlatformInvoiceRepository(db), new NotificationService(db), db);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.SetUserActiveAsync(blocked.UserId, true, admin));
        db.ChangeTracker.Clear(); Assert.False((await db.Users.SingleAsync(x => x.Id == blocked.UserId)).IsActive);
    }

    [Fact]
    public async Task Configured_limit_and_capacity_are_scoped_and_independent_of_list_filters()
    {
        await using var db = Open(); await Add(db, 0); await Add(db, 1, false); await Add(db, 2, true, false);
        var scope = new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
        var service = new StaffService(new DepotStaffRepository(db), scope, capacity: Microsoft.Extensions.Options.Options.Create(new DepotCapacityOptions { MaxActiveStaffPerDepot = 1 }));
        var capacity = await service.GetCapacityAsync(owner, depot);
        Assert.Equal(1, capacity.Used); Assert.Equal(1, capacity.Limit); Assert.Equal(0, capacity.Remaining); Assert.False(capacity.CanAdd);
        Assert.Equal(2, (await service.ListAsync(owner, depot, new() { Status = "INACTIVE" })).TotalCount);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.CreateAsync(owner, depot, Input()));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.GetCapacityAsync(admin, depot));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.CreateAsync(owner, Guid.NewGuid(), Input()));
    }
    [Fact]
    public async Task Legacy_over_limit_staff_are_preserved_and_can_be_disabled()
    {
        await using var db = Open(); DepotStaff? first = null;
        for (var i = 0; i < 11; i++) { var s = await Add(db, i); first ??= s; }
        var capacity = await Service(db).GetCapacityAsync(owner, depot);
        Assert.Equal(11, capacity.Used); Assert.Equal(0, capacity.Remaining); Assert.False(capacity.CanAdd);
        await Service(db).UpdateAsync(owner, depot, first!.Id, new() { FullName = "Vẫn sửa thông tin", Phone = "0901234567", IsActive = true });
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).CreateAsync(owner, depot, Input()));
        Assert.Equal(11, await db.DepotStaffs.CountAsync(x => x.DepotId == depot));
        await Service(db).UpdateAsync(owner, depot, first.Id, new() { FullName = first.User.FullName, Phone = "0901234567", IsActive = false });
        Assert.Equal(10, (await Service(db).GetCapacityAsync(owner, depot)).Used);
    }
    [Fact]
    public async Task Admin_activation_and_owner_creation_share_the_same_depot_lock()
    {
        Guid userId;
        await using (var db = Open()) { for (var i = 0; i < 9; i++) await Add(db, i); userId = (await Add(db, 11, true, false)).UserId; }
        async Task<bool> Write(bool activate)
        {
            await using var db = Open();
            try
            {
                if (activate) await new AdminService(new UserRepository(db), new MarketPriceRepository(db), new AuditLogRepository(db), new PlatformInvoiceRepository(db), new NotificationService(db), db).SetUserActiveAsync(userId, true, admin);
                else await Service(db).CreateAsync(owner, depot, Input());
                return true;
            }
            catch (DepotConflictException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Write(false), Write(true)), x => x);
        await using var verify = Open(); Assert.Equal(10, await verify.DepotStaffs.CountAsync(x => x.DepotId == depot && x.IsActive && x.User.IsActive));
    }
}
