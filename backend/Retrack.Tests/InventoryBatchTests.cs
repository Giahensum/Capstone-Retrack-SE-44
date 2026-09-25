using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed class InventoryBatchTests : IAsyncLifetime
{
    private readonly Guid ownerId = Guid.NewGuid();
    private readonly Guid depotId = Guid.NewGuid();
    private readonly Guid sellerId = Guid.NewGuid();
    private readonly Guid factoryOwnerId = Guid.NewGuid();
    private static AppDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION") ?? throw new InvalidOperationException("RETRACK_TEST_CONNECTION required");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test") throw new InvalidOperationException("Test database required");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
    private static BatchService Batches(AppDbContext db) => new(db, new DepotService(db), new InventoryService(db, new DepotService(db)));
    public async Task InitializeAsync()
    {
        await using var db = Open();
        await TestDatabase.MigrateAsync(db);
        db.Users.AddRange(new User { Id = ownerId, Email = $"batch-{ownerId}@test.invalid", Role = "DEPOT_OWNER", FullName = "Owner" },
            new User { Id = sellerId, Email = $"batch-{sellerId}@test.invalid", Role = "SELLER", FullName = "Seller" });
        db.Depots.Add(new Depot { Id = depotId, OwnerId = ownerId, Name = "Test stock", Address = "Test" });
        var receipt = new PickupRequest { SellerId = sellerId, TargetDepotId = depotId, Address = "Test", Status = "DONE" };
        receipt.Items.Add(new PickupRequestItem { MaterialType = "PET", WeightKg = 100, PricePerKg = 10, SubTotal = 1000 });
        db.PickupRequests.Add(receipt);
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open();
        await db.TransportJobs.Where(t => t.Batch.DepotId == depotId).ExecuteDeleteAsync();
        await db.InventoryBatches.Where(b => b.DepotId == depotId).ExecuteDeleteAsync();
        await db.FactoryDepotPartnerships.Where(p => p.DepotId == depotId).ExecuteDeleteAsync();
        await db.Factories.Where(f => f.OwnerId == factoryOwnerId).ExecuteDeleteAsync();
        await db.PickupRequests.Where(p => p.TargetDepotId == depotId).ExecuteDeleteAsync();
        var staffUsers = await db.DepotStaffs.Where(s => s.DepotId == depotId).Select(s => s.UserId).ToListAsync();
        await db.DepotStaffs.Where(s => s.DepotId == depotId).ExecuteDeleteAsync();
        await db.Users.Where(u => staffUsers.Contains(u.Id)).ExecuteDeleteAsync();
        await db.PlatformFeeInvoices.Where(i => i.OwnerId == ownerId).ExecuteDeleteAsync();
        await db.Depots.Where(d => d.Id == depotId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == ownerId || u.Id == sellerId || u.Id == factoryOwnerId).ExecuteDeleteAsync();
    }
    private static CreateDepotBatchDto Input(decimal kg = 60) => new() { OperationId = Guid.NewGuid(), MaterialType = "PET", WeightKg = kg };
    [Fact] public async Task Profile_update_persists_and_rejects_invalid_coordinates()
    {
        await using var db = Open(); var service = new DepotService(db);
        var dto = new UpdateDepotProfileDto { Name = "Updated", Address = "Address", ContactPhone = "0901234567", Latitude = 10, Longitude = 106 };
        var profile = await service.UpdateProfileAsync(ownerId, depotId, dto);
        Assert.Equal("Updated", profile.Name);
        dto.Latitude = 100;
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.UpdateProfileAsync(ownerId, depotId, dto));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.GetProfileAsync(sellerId, depotId));
    }
    [Fact] public async Task Staff_account_membership_validation_and_disable_persist()
    {
        await using var db = Open(); var service = new StaffService(db, new DepotService(db));
        var input = new CreateStaffDto { Email = $"staff-{Guid.NewGuid()}@test.invalid", FullName = "Employee", Phone = "0901234567", Password = "TestOnly123!", Role = "DEPOT_EMPLOYEE" };
        var staff = await service.CreateAsync(ownerId, depotId, input);
        Assert.Equal(depotId, (await db.DepotStaffs.FindAsync(staff.Id))!.DepotId);
        Assert.True(BCrypt.Net.BCrypt.Verify(input.Password, (await db.Users.FindAsync(staff.UserId))!.PasswordHash));
        await Assert.ThrowsAsync<DepotConflictException>(() => service.CreateAsync(ownerId, depotId, input));
        input.Role = "ADMIN";
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateAsync(ownerId, depotId, input));
        await service.UpdateAsync(ownerId, depotId, staff.Id, new() { FullName = "Updated", Phone = input.Phone, IsActive = false });
        Assert.False((await db.Users.FindAsync(staff.UserId))!.IsActive);
    }
    [Fact] public async Task Reports_execute_on_Postgres_and_are_owner_scoped()
    {
        await using var db = Open(); var scope = new DepotService(db);
        var staff = new DepotStaff { DepotId = depotId, UserId = sellerId, StaffType = "DEPOT_EMPLOYEE" };
        db.DepotStaffs.Add(staff); await db.SaveChangesAsync();
        var service = new DepotReportService(db, scope, new InventoryService(db, scope));
        Assert.Equal(100, (await service.DashboardAsync(ownerId, depotId)).AvailableKg);
        Assert.Equal(0, (await service.RevenueAsync(ownerId, depotId, new())).Revenue);
        Assert.Single((await service.PerformanceAsync(ownerId, depotId, new(), new())).Items);
        Assert.Empty((await service.HistoryAsync(ownerId, depotId, staff.Id, new(), new())).Items);
        Assert.Empty((await service.FeesAsync(ownerId, depotId, new(), new())).Items);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.DashboardAsync(sellerId, depotId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RevenueAsync(ownerId, depotId, new() { From = new(2026, 2, 1), To = new(2026, 1, 1) }));
    }
    [Fact] public async Task Invoice_confirmation_waits_for_admin_and_is_idempotent()
    {
        await using var db = Open(); var scope = new DepotService(db);
        var invoice = new PlatformFeeInvoice { OwnerId = ownerId, PeriodStart = new(2026, 8, 1), Amount = 100 };
        db.PlatformFeeInvoices.Add(invoice); await db.SaveChangesAsync();
        var service = new DepotReportService(db, scope, new InventoryService(db, scope));
        await service.ConfirmInvoiceAsync(ownerId, depotId, invoice.Id, new() { PaymentProofUrl = "https://example.com/test.png" });
        await service.ConfirmInvoiceAsync(ownerId, depotId, invoice.Id, new() { PaymentProofUrl = "https://example.com/test.png" });
        Assert.Equal("SUBMITTED", (await db.PlatformFeeInvoices.FindAsync(invoice.Id))!.Status);
        Assert.Equal(100, (await service.FeeSummaryAsync(ownerId, depotId, new())).SubmittedInvoiceAmount);
        Assert.Single((await service.InvoicesAsync(ownerId, depotId, new())).Items);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.ConfirmInvoiceAsync(ownerId, depotId, invoice.Id, new() { PaymentProofUrl = "https://example.com/other.png" }));
    }
    [Fact] public async Task Concurrent_exports_cannot_reserve_more_than_available_stock()
    {
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
            await using var db = Open();
            try { await Batches(db).CreateAsync(ownerId, depotId, Input()); return true; }
            catch (DepotConflictException) { return false; }
        }));
        Assert.Single(outcomes, x => x);
        await using var verify = Open();
        var stock = Assert.Single(await new InventoryService(verify, new DepotService(verify)).GetAsync(ownerId, depotId));
        Assert.Equal(40, stock.AvailableKg);
        Assert.Equal(60, stock.ReservedKg);
    }
    [Fact] public async Task Repeated_create_is_idempotent_and_changed_payload_conflicts()
    {
        var input = Input();
        await using (var db = Open()) await Batches(db).CreateAsync(ownerId, depotId, input);
        await using (var db = Open()) Assert.Equal(input.OperationId, (await Batches(db).CreateAsync(ownerId, depotId, input)).Id);
        input.WeightKg = 30;
        await using var verify = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Batches(verify).CreateAsync(ownerId, depotId, input));
        Assert.Equal(1, await verify.InventoryBatches.CountAsync(b => b.DepotId == depotId));
        Assert.Matches(@"^LO-\d{4}-\d{3,}$", (await verify.InventoryBatches.SingleAsync(b => b.DepotId == depotId)).Code!);
    }
    [Theory]
    [InlineData("APPROVED", "TRANSPORT_READY", 1)]
    [InlineData("PENDING", "PENDING_APPROVAL", 0)]
    [InlineData("BLOCKED", null, 0)]
    public async Task Factory_relationship_controls_batch_and_transport_atomically(string relationship, string? expectedStatus, int jobs)
    {
        await using var db = Open();
        db.Users.Add(new User { Id = factoryOwnerId, Email = $"factory-{factoryOwnerId}@test.invalid", Role = "FACTORY", FullName = "Factory owner" });
        var factory = new Factory { OwnerId = factoryOwnerId, Name = "Test factory", Address = "Test" };
        db.Factories.Add(factory);
        db.FactoryDepotPartnerships.Add(new FactoryDepotPartnership { DepotId = depotId, FactoryId = factory.Id, Status = relationship });
        await db.SaveChangesAsync();
        var input = Input(); input.TargetFactoryId = factory.Id;
        if (expectedStatus == null)
        {
            await Assert.ThrowsAsync<DepotConflictException>(() => Batches(db).CreateAsync(ownerId, depotId, input));
            Assert.False(await db.InventoryBatches.AnyAsync(b => b.Id == input.OperationId));
        }
        else
        {
            Assert.Equal(expectedStatus, (await Batches(db).CreateAsync(ownerId, depotId, input)).Status);
            await Assert.ThrowsAsync<DepotConflictException>(() => Batches(db).CancelAsync(ownerId, depotId, input.OperationId));
        }
        Assert.Equal(jobs, await db.TransportJobs.CountAsync(t => t.BatchId == input.OperationId));
    }
    [Fact] public async Task Cancel_restores_stock_once()
    {
        var input = Input();
        await using (var db = Open()) await Batches(db).CreateAsync(ownerId, depotId, input);
        await using (var db = Open()) await Batches(db).CancelAsync(ownerId, depotId, input.OperationId);
        await using (var db = Open()) await Batches(db).CancelAsync(ownerId, depotId, input.OperationId);
        await using var verify = Open();
        Assert.Equal(100, Assert.Single(await new InventoryService(verify, new DepotService(verify)).GetAsync(ownerId, depotId)).AvailableKg);
    }
    [Fact] public async Task Unconfirmed_pickups_do_not_increase_stock()
    {
        await using var db = Open();
        await db.PickupRequests.Where(p => p.TargetDepotId == depotId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "PAYMENT_SENT"));
        Assert.Empty(await new InventoryService(db, new DepotService(db)).GetAsync(ownerId, depotId));
        await Assert.ThrowsAsync<DepotConflictException>(() => Batches(db).CreateAsync(ownerId, depotId, Input()));
    }
    [Theory] [InlineData(0)] [InlineData(-1)] public async Task Non_positive_weight_is_rejected(decimal weight)
    {
        await using var db = Open();
        await Assert.ThrowsAsync<ArgumentException>(() => Batches(db).CreateAsync(ownerId, depotId, Input(weight)));
        Assert.False(await db.InventoryBatches.AnyAsync(b => b.DepotId == depotId));
    }
    [Fact] public async Task Other_owner_cannot_read_inventory_or_create_or_cancel()
    {
        await using var db = Open();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => new InventoryService(db, new DepotService(db)).GetAsync(sellerId, depotId));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Batches(db).CreateAsync(sellerId, depotId, Input()));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Batches(db).CancelAsync(sellerId, depotId, Guid.NewGuid()));
    }
    [Fact] public async Task Missing_factory_does_not_reserve_stock()
    {
        await using var db = Open();
        var input = Input(); input.TargetFactoryId = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => Batches(db).CreateAsync(ownerId, depotId, input));
        Assert.False(await db.InventoryBatches.AnyAsync(b => b.DepotId == depotId));
    }
}
