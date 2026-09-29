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
    private static BatchService Batches(AppDbContext db) => new(new Retrack.API.Repositories.DepotBatchRepository(db), new Retrack.API.Repositories.DepotUnitOfWork(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db)), new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))));
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
        await db.PlatformInvoices.Where(i => i.PayerId == ownerId).ExecuteDeleteAsync();
        await db.Depots.Where(d => d.Id == depotId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == ownerId || u.Id == sellerId || u.Id == factoryOwnerId).ExecuteDeleteAsync();
    }
    private static CreateDepotBatchDto Input(decimal kg = 60) => new() { OperationId = Guid.NewGuid(), MaterialType = "PET", WeightKg = kg };

    private sealed class ProofImages : Retrack.API.Services.Interfaces.ICloudinaryService
    {
        public int Uploads { get; private set; }
        public Task<string> UploadImageAsync(Stream stream, string name) { Uploads++; return Task.FromResult("https://example.com/proof.png"); }
        public Task<string> UploadAvatarAsync(Stream stream, string name) => throw new NotSupportedException();
        public Task<bool> DeleteImageAsync(string id) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("image/svg+xml", false)]
    [InlineData("image/png", false)]
    [InlineData("image/png", true)]
    public async Task Proof_upload_validates_image_bytes_and_owner(string type, bool valid)
    {
        await using var db = Open();
        var images = new ProofImages();
        var service = new DepotProofService(new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db)), images);
        byte[] bytes = valid ? [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0] : [60, 115, 118, 103, 62];
        var file = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "proof.png")
            { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = type };
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.UploadAsync(sellerId, depotId, file));
        Assert.Equal(0, images.Uploads);
        if (valid) Assert.Equal("https://example.com/proof.png", await service.UploadAsync(ownerId, depotId, file));
        else await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(ownerId, depotId, file));
        Assert.Equal(valid ? 1 : 0, images.Uploads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10485761)]
    public async Task Proof_upload_rejects_empty_or_oversized_file_before_provider(long length)
    {
        await using var db = Open();
        var images = new ProofImages();
        var service = new DepotProofService(new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db)), images);
        var file = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(), 0, length, "file", "proof.png")
            { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = "image/png" };
        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(ownerId, depotId, file));
        Assert.Equal(0, images.Uploads);
    }

    private sealed class NoNotifications : Retrack.API.Services.Interfaces.INotificationService
    {
        public Task SendAsync(Guid userId, string title, string message) => Task.CompletedTask;
        public Task<IEnumerable<object>> GetByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<object>>([]);
        public Task MarkAsReadAsync(Guid notificationId) => Task.CompletedTask;
    }

    [Fact]
    public async Task Concurrent_invoice_generation_is_unique_and_uses_Vietnam_period_on_Postgres()
    {
        Guid sourceId;
        await using (var db = Open())
        {
            sourceId = await db.PickupRequests.Where(p => p.TargetDepotId == depotId).Select(p => p.Id).SingleAsync();
            db.PlatformTransactions.Add(new PlatformTransaction { SourceType = "PICKUP_REQUEST", SourceId = sourceId,
                FeeAmount = 123, CreatedAt = new DateTime(1980, 8, 31, 17, 0, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        }
        try
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
                await using var db = Open();
                var admin = new Retrack.API.Services.AdminService(new Retrack.API.Repositories.UserRepository(db),
                    new Retrack.API.Repositories.MarketPriceRepository(db), new Retrack.API.Repositories.AuditLogRepository(db),
                    new Retrack.API.Repositories.PlatformInvoiceRepository(db), new NoNotifications(), db);
                return await admin.GenerateMonthlyInvoicesAsync(1980, 9, ownerId);
            }));
            Assert.Single(results.SelectMany(x => x), i => i.PayerId == ownerId);
            await using var verify = Open();
            var invoice = Assert.Single(await verify.PlatformInvoices.Where(i => i.PayerId == ownerId).ToListAsync());
            Assert.Equal(123, invoice.TotalFeeAmount);
        }
        finally
        {
            await using var db = Open();
            await db.PlatformTransactions.Where(t => t.SourceId == sourceId).ExecuteDeleteAsync();
            await db.AuditLogs.Where(a => a.UserId == ownerId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Depot_reads_admin_invoice_and_submits_proof_for_admin_reconciliation()
    {
        await using var db = Open();
        var invoice = new PlatformInvoice { PayerId = ownerId, PeriodYear = 2026, PeriodMonth = 9, TotalFeeAmount = 5000 };
        db.PlatformInvoices.Add(invoice);
        await db.SaveChangesAsync();
        try
        {
            var service = new DepotReportService(db, new Retrack.API.Repositories.DepotReportRepository(db), new Retrack.API.Repositories.DepotStaffRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db)), new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))));
            Assert.Equal(invoice.Id, Assert.Single((await service.InvoicesAsync(ownerId, depotId, new())).Items).Id);
            var proof = new PaymentProofDto { PaymentProofUrl = "https://example.com/local-invoice-test.png" };
            await service.ConfirmInvoiceAsync(ownerId, depotId, invoice.Id, proof);
            await service.ConfirmInvoiceAsync(ownerId, depotId, invoice.Id, proof);
            Assert.Equal("SUBMITTED", (await db.PlatformInvoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id)).Status);
            Assert.Equal(5000, (await service.FeeSummaryAsync(ownerId, depotId, new())).SubmittedInvoiceAmount);
            await Assert.ThrowsAsync<DepotForbiddenException>(() => service.ConfirmInvoiceAsync(sellerId, depotId, invoice.Id, proof));
            var admin = new Retrack.API.Services.AdminService(new Retrack.API.Repositories.UserRepository(db),
                new Retrack.API.Repositories.MarketPriceRepository(db), new Retrack.API.Repositories.AuditLogRepository(db),
                new Retrack.API.Repositories.PlatformInvoiceRepository(db), new Retrack.API.Services.Shared.NotificationService(db), db);
            var paid = await admin.MarkInvoicePaidAsync(invoice.Id, ownerId);
            var persistedPaidAt = await db.PlatformInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.PaidAt).SingleAsync();
            var repeated = await admin.MarkInvoicePaidAsync(invoice.Id, ownerId);
            Assert.NotNull(paid.PaidAt);
            Assert.Equal(persistedPaidAt, repeated.PaidAt);
            var displayed = Assert.Single((await service.InvoicesAsync(ownerId, depotId, new())).Items);
            Assert.Equal("PAID", displayed.Status);
            Assert.Equal(proof.PaymentProofUrl, displayed.PaymentProofUrl);
            Assert.Equal(0, (await service.FeeSummaryAsync(ownerId, depotId, new())).SubmittedInvoiceAmount);
        }
        finally
        {
            await db.AuditLogs.Where(a => a.UserId == ownerId).ExecuteDeleteAsync();
            await db.PlatformInvoices.Where(i => i.Id == invoice.Id).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Factory_can_reject_only_its_direct_offer(bool direct)
    {
        await using var db = Open();
        db.Users.Add(new User { Id = factoryOwnerId, Email = $"factory-{factoryOwnerId}@test.invalid", Role = "FACTORY", FullName = "Nhà máy thử" });
        var factory = new Factory { OwnerId = factoryOwnerId, Name = "Nhà máy thử", Address = "Thử nghiệm" };
        db.Factories.Add(factory);
        await db.SaveChangesAsync();
        var input = Input(); if (direct) input.TargetFactoryId = factory.Id;
        await Batches(db).CreateAsync(ownerId, depotId, input);
        var result = await new Retrack.API.Services.Factory.FactoryMarketService(db).RejectOfferAsync(factoryOwnerId, input.OperationId, new() { Reason = "Không phù hợp" }, default);
        Assert.Equal(direct ? Retrack.API.Services.Shared.ServiceOutcome.Success : Retrack.API.Services.Shared.ServiceOutcome.Conflict, result.Outcome);
        var stock = Assert.Single(await new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))).GetAsync(ownerId, depotId));
        Assert.Equal(direct ? 100 : 40, stock.AvailableKg);
    }

    [Fact]
    public async Task Concurrent_factory_accept_and_depot_cancel_leave_one_consistent_outcome()
    {
        var input = Input();
        await using (var db = Open())
        {
            db.Users.Add(new User { Id = factoryOwnerId, Email = $"factory-{factoryOwnerId}@test.invalid", Role = "FACTORY", FullName = "Nhà máy thử" });
            var factory = new Factory { OwnerId = factoryOwnerId, Name = "Nhà máy thử", Address = "Thử nghiệm" };
            db.Factories.Add(factory);
            db.FactoryDepotPartnerships.Add(new FactoryDepotPartnership { DepotId = depotId, FactoryId = factory.Id, Status = "APPROVED" });
            await db.SaveChangesAsync();
            await Batches(db).CreateAsync(ownerId, depotId, input);
        }
        var accept = Task.Run(async () => {
            await using var db = Open();
            return (await new Retrack.API.Services.Factory.FactoryMarketService(db).AcceptAsync(factoryOwnerId, input.OperationId, new() { AgreedPricePerKg = 10 }, default)).Outcome == Retrack.API.Services.Shared.ServiceOutcome.Success;
        });
        var cancel = Task.Run(async () => {
            await using var db = Open();
            try { await Batches(db).CancelAsync(ownerId, depotId, input.OperationId); return true; }
            catch (DepotConflictException) { return false; }
        });
        Assert.Single(await Task.WhenAll(accept, cancel), success => success);
        await using var verify = Open();
        var batch = await verify.InventoryBatches.SingleAsync(b => b.Id == input.OperationId);
        Assert.Equal(batch.Status == "ACCEPTED" ? 1 : 0, await verify.TransportJobs.CountAsync(t => t.BatchId == batch.Id));
        Assert.Contains(batch.Status, new[] { "ACCEPTED", "CANCELLED" });
    }

    [Fact]
    public async Task Pending_depot_offer_is_visible_and_accepted_by_the_selected_factory()
    {
        await using var db = Open();
        db.Users.Add(new User { Id = factoryOwnerId, Email = $"factory-{factoryOwnerId}@test.invalid", Role = "FACTORY", FullName = "Nhà máy thử" });
        var factory = new Factory { OwnerId = factoryOwnerId, Name = "Nhà máy thử", Address = "Thử nghiệm" };
        db.Factories.Add(factory);
        await db.SaveChangesAsync();
        var input = Input(); input.TargetFactoryId = factory.Id;
        await Batches(db).CreateAsync(ownerId, depotId, input);
        var market = new Retrack.API.Services.Factory.FactoryMarketService(db);
        var offers = await market.BatchesAsync(factoryOwnerId, new() { DirectOnly = true }, default);
        Assert.Contains(offers.Data!.Items, b => b.Id == input.OperationId);
        var partner = await db.FactoryDepotPartnerships.SingleAsync(p => p.DepotId == depotId);
        partner.Status = "APPROVED";
        await db.SaveChangesAsync();
        var accepted = await market.AcceptAsync(factoryOwnerId, input.OperationId, new() { AgreedPricePerKg = 10 }, default);
        Assert.Equal("ACCEPTED", accepted.Data!.Status);
        Assert.Single(await db.TransportJobs.Where(t => t.BatchId == input.OperationId).ToListAsync());
    }

    [Theory]
    [InlineData("ACCEPTED", "PICKED_UP", 60, 0)]
    [InlineData("RECEIVED", "DELIVERED", 60, 0)]
    [InlineData("WEIGHED", "DELIVERED", 60, 0)]
    [InlineData("REJECTED", null, 0, 0)]
    [InlineData("REJECTED", "DELIVERED", 60, 0)]
    public async Task Inventory_uses_transport_evidence_for_factory_states(string status, string? transport, decimal exported, decimal reserved)
    {
        await using var db = Open();
        var batch = new InventoryBatch { DepotId = depotId, MaterialType = "PET", DeclaredWeightKg = 60, Status = status };
        db.InventoryBatches.Add(batch);
        if (transport != null) db.TransportJobs.Add(new TransportJob { BatchId = batch.Id, Status = transport });
        await db.SaveChangesAsync();
        var row = Assert.Single(await new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))).GetAsync(ownerId, depotId));
        Assert.Equal(exported, row.ExportedKg);
        Assert.Equal(reserved, row.ReservedKg);
        Assert.Equal(100 - exported - reserved, row.AvailableKg);
    }

    [Fact]
    public async Task Revenue_uses_settlement_date_and_counts_a_batch_only_once()
    {
        await using var db = Open();
        var date = new DateTime(2026, 9, 27, 18, 0, 0, DateTimeKind.Utc);
        db.InventoryBatches.Add(new InventoryBatch { DepotId = depotId, MaterialType = "PET", DeclaredWeightKg = 10,
            Status = "COMPLETED", NetAmount = 950, SettledAt = date });
        await db.SaveChangesAsync();
        var service = new DepotReportService(db, new Retrack.API.Repositories.DepotReportRepository(db), new Retrack.API.Repositories.DepotStaffRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db)), new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))));
        var report = await service.RevenueAsync(ownerId, depotId, new() { From = new(2026, 9, 28), To = new(2026, 9, 28) });
        Assert.Equal(950, report.Revenue);
        Assert.Equal(new DateOnly(2026, 9, 28), Assert.Single(report.Points).Date);
        Assert.Equal(0, (await service.RevenueAsync(ownerId, depotId, new() { From = new(2026, 9, 27), To = new(2026, 9, 27) })).Revenue);
    }
    [Fact] public async Task Profile_update_persists_and_rejects_invalid_coordinates()
    {
        await using var db = Open(); var service = new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db));
        var dto = new UpdateDepotProfileDto { Name = "Updated", Address = "Address", ContactPhone = "0901234567", Latitude = 10, Longitude = 106 };
        var profile = await service.UpdateProfileAsync(ownerId, depotId, dto);
        Assert.Equal("Updated", profile.Name);
        dto.Latitude = 100;
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.UpdateProfileAsync(ownerId, depotId, dto));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.GetProfileAsync(sellerId, depotId));
    }
    [Fact] public async Task Staff_account_membership_validation_and_disable_persist()
    {
        await using var db = Open(); var service = new StaffService(new Retrack.API.Repositories.DepotStaffRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db)));
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
        await using var db = Open(); var scope = new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db));
        var staff = new DepotStaff { DepotId = depotId, UserId = sellerId, StaffType = "DEPOT_EMPLOYEE" };
        db.DepotStaffs.Add(staff); await db.SaveChangesAsync();
        var service = new DepotReportService(db, new Retrack.API.Repositories.DepotReportRepository(db), new Retrack.API.Repositories.DepotStaffRepository(db), scope, new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), scope));
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
        await using var db = Open(); var scope = new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db));
        var invoice = new PlatformInvoice { PayerId = ownerId, PeriodYear = 2026, PeriodMonth = 8, TotalFeeAmount = 100 };
        db.PlatformInvoices.Add(invoice); await db.SaveChangesAsync();
        var service = new DepotReportService(db, new Retrack.API.Repositories.DepotReportRepository(db), new Retrack.API.Repositories.DepotStaffRepository(db), scope, new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), scope));
        await service.ConfirmInvoiceAsync(ownerId, depotId, invoice.Id, new() { PaymentProofUrl = "https://example.com/test.png" });
        await service.ConfirmInvoiceAsync(ownerId, depotId, invoice.Id, new() { PaymentProofUrl = "https://example.com/test.png" });
        Assert.Equal("SUBMITTED", (await db.PlatformInvoices.FindAsync(invoice.Id))!.Status);
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
        var stock = Assert.Single(await new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(verify), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(verify), new Retrack.API.Repositories.DepotPaymentReadRepository(verify))).GetAsync(ownerId, depotId));
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
        Assert.Equal(100, Assert.Single(await new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(verify), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(verify), new Retrack.API.Repositories.DepotPaymentReadRepository(verify))).GetAsync(ownerId, depotId)).AvailableKg);
    }
    [Fact] public async Task Unconfirmed_pickups_do_not_increase_stock()
    {
        await using var db = Open();
        await db.PickupRequests.Where(p => p.TargetDepotId == depotId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "PAYMENT_SENT"));
        Assert.Empty(await new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))).GetAsync(ownerId, depotId));
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
        await Assert.ThrowsAsync<DepotForbiddenException>(() => new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))).GetAsync(sellerId, depotId));
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
