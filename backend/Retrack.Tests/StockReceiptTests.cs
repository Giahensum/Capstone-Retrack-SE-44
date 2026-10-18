using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;
using Xunit;

namespace Retrack.Tests;

public sealed class StockReceiptTests : IAsyncLifetime
{
    private readonly Guid owner = Guid.NewGuid(), depot = Guid.NewGuid(), employee = Guid.NewGuid();
    private static AppDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION") ?? throw new InvalidOperationException("RETRACK_TEST_CONNECTION required");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test") throw new InvalidOperationException("Test database required");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
    private static DepotService Scope(AppDbContext db) => new(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
    private static DepotStockService Stock(AppDbContext db) => new(new DepotStockRepository(db), new DepotBatchRepository(db), new DepotUnitOfWork(db), Scope(db));
    private static InventoryService Inventory(AppDbContext db) => new(new DepotInventoryRepository(db), Scope(db));
    private static BatchService Batches(AppDbContext db) => new(new DepotBatchRepository(db), new DepotUnitOfWork(db), Scope(db), Inventory(db), new NoImages(), Stock(db));
    private static DepotReportService Reports(AppDbContext db) => new(new DepotUnitOfWork(db), new DepotReportRepository(db), new DepotStaffRepository(db), Scope(db), Inventory(db));
    public async Task InitializeAsync()
    {
        await using var db = Open(); await TestDatabase.MigrateAsync(db);
        db.Users.AddRange(new User { Id = owner, Email = $"stock-{owner}@test.invalid", FullName = "Chủ kho test", Role = "DEPOT_OWNER" },
            new User { Id = employee, Email = $"stock-{employee}@test.invalid", FullName = "Nhân viên test", Role = "DEPOT_EMPLOYEE" });
        db.Depots.Add(new Depot { Id = depot, OwnerId = owner, Name = "Kho thử nhập nguồn", Address = "Đà Nẵng" });
        db.DepotStaffs.Add(new DepotStaff { DepotId = depot, UserId = employee, StaffType = "DEPOT_EMPLOYEE" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open();
        await db.DepotReuseSales.Where(x => x.Line.Receipt.DepotId == depot).ExecuteDeleteAsync();
        await db.DepotStockReceipts.Where(x => x.DepotId == depot).ExecuteDeleteAsync();
        await db.TransportJobs.Where(x => x.Batch.DepotId == depot).ExecuteDeleteAsync();
        await db.InventoryBatches.Where(x => x.DepotId == depot).ExecuteDeleteAsync();
        await db.DepotStaffs.Where(x => x.DepotId == depot).ExecuteDeleteAsync();
        await db.Depots.Where(x => x.Id == depot).ExecuteDeleteAsync();
        await db.Users.Where(x => x.Id == owner || x.Id == employee).ExecuteDeleteAsync();
    }
    private static CreateStockReceiptDto Input(decimal quantity = 1000, decimal unitCost = 5000, bool reusable = false) => new() {
        OperationId = Guid.NewGuid(), Reference = "Phiếu thử " + Guid.NewGuid().ToString("N"), ReceivedAt = DateTime.UtcNow.AddMinutes(-2),
        PurchaseCost = quantity * unitCost, CostsConfirmed = true,
        Lines = [new() { Kind = reusable ? "REUSABLE" : "MATERIAL", MaterialType = reusable ? null : "IRON",
            Name = reusable ? "Ghế" : "Sắt", Unit = reusable ? "cái" : "kg", Quantity = quantity, ReferencePrice = unitCost }] };
    private async Task<StockReceiptDto> Posted(AppDbContext db, CreateStockReceiptDto input)
    {
        var draft = await Stock(db).CreateAsync(owner, depot, input);
        return await Stock(db).ReviewAsync(owner, depot, draft.Id, "POSTED");
    }
    [Fact]
    public async Task Cost_report_matches_settled_fifo_and_partial_reuse_sale_without_double_subtracting_fees()
    {
        await using var db = Open(); await Posted(db, Input(50)); await Posted(db, Input(50));
        var chairs = await Posted(db, Input(10, 300000, true));
        await Stock(db).SellAsync(owner, depot, chairs.Lines.Single().Id, new(Guid.NewGuid(), 3, 450000, 50000, "Đã giao"));
        var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 100 });
        await Stock(db).AddBatchExpenseAsync(owner, depot, batch.Id, new(Guid.NewGuid(), "Xe", 50000));
        await Stock(db).AddBatchExpenseAsync(owner, depot, batch.Id, new(Guid.NewGuid(), "Bốc xếp", 50000));
        var entity = await db.InventoryBatches.SingleAsync(x => x.Id == batch.Id);
        entity.Status = "PAID"; entity.GrossAmount = 1000000; entity.PlatformFeeAmount = 100000; entity.NetAmount = 900000; entity.SettledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var report = await Reports(db).CostReportAsync(owner, depot, new());
        Assert.Equal(2, report.TransactionCount); Assert.Equal(0, report.IncompleteCount);
        Assert.Equal(2250000, report.NetRevenue); Assert.Equal(1400000, report.KnownCostOfGoods);
        Assert.Equal(150000, report.SellingCosts); Assert.Equal(700000, report.PretaxDifference);
        Assert.Equal(100000, report.PlatformFees);
        Assert.Equal(300000, report.Channels.Single(x => x.Channel == "FACTORY").PretaxDifference);
        Assert.Equal(400000, report.Channels.Single(x => x.Channel == "REUSABLE").PretaxDifference);
    }
    [Fact]
    public async Task Cost_report_uses_one_settlement_per_batch_and_never_includes_another_owned_depot()
    {
        await using var db = Open(); var factoryUser = Guid.NewGuid(); var factoryId = Guid.NewGuid(); var otherDepot = Guid.NewGuid();
        db.Users.Add(new User { Id = factoryUser, Role = "FACTORY", Email = $"cost-{factoryUser}@test.invalid" });
        db.Factories.Add(new Factory { Id = factoryId, OwnerId = factoryUser, Name = "Nhà máy kiểm thử", Address = "Đà Nẵng" });
        db.Depots.Add(new Depot { Id = otherDepot, OwnerId = owner, Name = "Kho khác cùng chủ", Address = "Đà Nẵng" });
        await db.SaveChangesAsync();
        try
        {
            await Posted(db, Input(100));
            var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 100 });
            var settled = await db.InventoryBatches.SingleAsync(x => x.Id == batch.Id);
            settled.Status = "PAID"; settled.SettledAt = DateTime.UtcNow; settled.NetAmount = 900000;
            var legacy = new InventoryBatch { DepotId = depot, Status = "VERIFIED", MaterialType = "IRON", DeclaredWeightKg = 100 };
            db.InventoryBatches.AddRange(legacy, new InventoryBatch { DepotId = otherDepot, Status = "PAID", MaterialType = "IRON",
                DeclaredWeightKg = 100, SettledAt = DateTime.UtcNow, NetAmount = 9999999 });
            db.BatchQualityChecks.AddRange(new BatchQualityCheck { BatchId = settled.Id, FactoryId = factoryId, IsAccepted = true,
                PaymentProofUrl = "https://example.test/paid", NetAmount = 999000 },
                new BatchQualityCheck { Batch = legacy, FactoryId = factoryId, IsAccepted = true,
                PaymentProofUrl = "https://example.test/legacy", NetAmount = 700000 });
            await db.SaveChangesAsync();
            var report = await Reports(db).CostReportAsync(owner, depot, new());
            Assert.Equal(2, report.TransactionCount); Assert.Equal(1600000, report.NetRevenue);
            Assert.Equal(1, report.IncompleteCount); Assert.Equal(500000, report.KnownCostOfGoods); Assert.Null(report.PretaxDifference);
        }
        finally
        {
            await db.BatchQualityChecks.Where(x => x.FactoryId == factoryId).ExecuteDeleteAsync();
            await db.InventoryBatches.Where(x => x.DepotId == otherDepot).ExecuteDeleteAsync();
            await db.Depots.Where(x => x.Id == otherDepot).ExecuteDeleteAsync();
            await db.Factories.Where(x => x.Id == factoryId).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Id == factoryUser).ExecuteDeleteAsync();
        }
    }
    [Fact]
    public async Task Cost_report_keeps_incomplete_settlement_unknown_instead_of_zero_revenue()
    {
        await using var db = Open(); await Posted(db, Input(100));
        var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 100 });
        var entity = await db.InventoryBatches.SingleAsync(x => x.Id == batch.Id);
        entity.Status = "PAID"; entity.SettledAt = DateTime.UtcNow; entity.NetAmount = null;
        await db.SaveChangesAsync();
        var report = await Reports(db).CostReportAsync(owner, depot, new());
        Assert.Equal(1, report.TransactionCount); Assert.Equal(1, report.IncompleteCount);
        Assert.Equal(500000, report.KnownCostOfGoods); Assert.Null(report.NetRevenue); Assert.Null(report.PretaxDifference);
    }
    [Fact]
    public async Task Cost_report_leaves_legacy_cost_unknown_and_does_not_count_open_or_cancelled_batches()
    {
        await using var db = Open();
        db.InventoryBatches.AddRange(new InventoryBatch { DepotId = depot, MaterialType = "IRON", DeclaredWeightKg = 100,
            Status = "PAID", GrossAmount = 1000000, PlatformFeeAmount = 100000, NetAmount = 900000, SettledAt = DateTime.UtcNow },
            new InventoryBatch { DepotId = depot, MaterialType = "IRON", DeclaredWeightKg = 100, Status = "MARKETPLACE" },
            new InventoryBatch { DepotId = depot, MaterialType = "IRON", DeclaredWeightKg = 100, Status = "CANCELLED", NetAmount = 9999999, SettledAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var report = await Reports(db).CostReportAsync(owner, depot, new());
        Assert.Equal(1, report.TransactionCount); Assert.Equal(1, report.IncompleteCount);
        Assert.Equal(900000, report.NetRevenue); Assert.Null(report.PretaxDifference);
        Assert.Null(report.Channels.Single(x => x.Channel == "FACTORY").PretaxDifference);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Reports(db).CostReportAsync(employee, depot, new()));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Reports(db).CostReportAsync(Guid.NewGuid(), depot, new()));
    }
    [Fact]
    public async Task Cost_report_filters_by_vietnam_day_and_does_not_convert_unknown_reuse_cost_to_zero()
    {
        await using var db = Open(); var input = Input(10, 0, true); input.CostsConfirmed = false;
        var receipt = await Posted(db, input);
        var sale = await Stock(db).SellAsync(owner, depot, receipt.Lines.Single().Id, new(Guid.NewGuid(), 1, 450000, 50000, "Đã giao"));
        var entity = await db.DepotReuseSales.SingleAsync(x => x.Id == sale.Id);
        entity.CreatedAt = new DateTime(2026, 10, 17, 17, 0, 0, DateTimeKind.Utc); await db.SaveChangesAsync();
        var report = await Reports(db).CostReportAsync(owner, depot, new() { From = new(2026, 10, 18), To = new(2026, 10, 18) });
        Assert.Equal(1, report.TransactionCount); Assert.Equal(450000, report.NetRevenue); Assert.Equal(1, report.IncompleteCount); Assert.Null(report.PretaxDifference);
        var previous = await Reports(db).CostReportAsync(owner, depot, new() { From = new(2026, 10, 17), To = new(2026, 10, 17) });
        Assert.Equal(0, previous.TransactionCount);
    }
    [Fact]
    public async Task Draft_does_not_increase_stock_and_post_retry_cannot_duplicate_stock()
    {
        await using var db = Open(); var input = Input();
        var draft = await Stock(db).CreateAsync(owner, depot, input);
        Assert.Empty(await Inventory(db).GetAsync(owner, depot));
        await Stock(db).CreateAsync(owner, depot, input);
        await Stock(db).ReviewAsync(owner, depot, draft.Id, "POSTED");
        await Stock(db).ReviewAsync(owner, depot, draft.Id, "POSTED");
        await using var verify = Open();
        Assert.Equal(1000, Assert.Single(await Inventory(verify).GetAsync(owner, depot)).AvailableKg);
        Assert.Single((await Stock(verify).ListAsync(owner, depot, new())).Items);
    }
    [Fact]
    public async Task Fifo_reserves_two_sources_and_cost_expense_retry_is_idempotent()
    {
        await using var db = Open();
        var first = Input(); first.ReceivedAt = DateTime.UtcNow.AddHours(-2);
        var second = Input(500, 6000); second.ReceivedAt = DateTime.UtcNow.AddHours(-1);
        await Posted(db, first); await Posted(db, second);
        var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 1300 });
        var expense = new CreateBatchExpenseDto(Guid.NewGuid(), "Công và xe giao hàng", 600000);
        await Stock(db).AddBatchExpenseAsync(owner, depot, batch.Id, expense);
        await Stock(db).AddBatchExpenseAsync(owner, depot, batch.Id, expense);
        var costs = await Stock(db).BatchCostsAsync(owner, depot, batch.Id);
        Assert.True(costs.HasCompleteCost); Assert.Equal(6800000, costs.CostOfGoods);
        Assert.Equal(600000, costs.SellingCosts); Assert.Null(costs.PretaxDifference);
        Assert.Equal(new decimal[] { 300, 1000 }, costs.Sources.Select(x => x.Quantity).Order());
        Assert.Equal(200, Assert.Single(await Inventory(db).GetAsync(owner, depot)).AvailableKg);
    }
    [Fact]
    public async Task Cancel_releases_source_allocations_for_next_batch()
    {
        await using var db = Open(); await Posted(db, Input(100));
        var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 90 });
        await Batches(db).CancelAsync(owner, depot, batch.Id);
        var next = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 100 });
        Assert.Equal(500000, (await Stock(db).BatchCostsAsync(owner, depot, next.Id)).CostOfGoods);
    }
    [Fact]
    public async Task Reusable_partial_sale_persists_quantity_and_never_creates_material_stock()
    {
        await using var db = Open(); var receipt = await Posted(db, Input(10, 300000, true));
        var line = receipt.Lines.Single(); var sale = new CreateReuseSaleDto(Guid.NewGuid(), 3, 450000, 0, "Đã giao ba ghế, bán ngoài app");
        var sold = await Stock(db).SellAsync(owner, depot, line.Id, sale);
        await Stock(db).SellAsync(owner, depot, line.Id, sale);
        Assert.Equal(450000, sold.PretaxDifference);
        await using var verify = Open();
        Assert.Equal(7, Assert.Single((await Stock(verify).ReusableAsync(owner, depot, new())).Items).AvailableQuantity);
        Assert.Single((await Stock(verify).SalesAsync(owner, depot, new())).Items);
        Assert.Empty(await Inventory(verify).GetAsync(owner, depot));
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(verify).SellAsync(owner, depot, line.Id, sale with { OperationId = Guid.NewGuid(), Quantity = 8 }));
    }
    [Fact]
    public async Task Employee_can_submit_receipt_but_cannot_post_or_access_owner_costs()
    {
        await using var db = Open(); var input = Input(); input.CostsConfirmed = false; input.PurchaseCost = 0; input.Lines[0].ReferencePrice = 0;
        var draft = await Stock(db).CreateAsync(employee, depot, input);
        await Stock(db).ReviewAsync(employee, depot, draft.Id, "SUBMITTED");
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).ReviewAsync(employee, depot, draft.Id, "POSTED"));
        await Stock(db).UpdateCostsAsync(owner, depot, draft.Id, new(5000000, true, [new(draft.Lines.Single().Id, 5000, 0)], []));
        var staffView = Assert.Single((await Stock(db).ListAsync(employee, depot, new())).Items);
        Assert.Null(staffView.Lines.Single().AllocatedCost);
        await Stock(db).ReviewAsync(owner, depot, draft.Id, "POSTED");
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).UpdateCostsAsync(owner, depot, draft.Id,
            new(0, false, [new(draft.Lines.Single().Id, 0, 0)], [])));
    }
    [Fact]
    public async Task Wrong_owner_and_inactive_membership_are_denied()
    {
        await using var db = Open();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).ListAsync(Guid.NewGuid(), depot, new()));
        var staff = await db.DepotStaffs.SingleAsync(x => x.DepotId == depot); staff.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).CreateAsync(employee, depot, Input()));
    }
    [Fact]
    public async Task Rejected_or_unknown_cost_receipts_cannot_be_reported_as_zero_cost_profit()
    {
        await using var db = Open(); var input = Input(); input.CostsConfirmed = false;
        var draft = await Stock(db).CreateAsync(owner, depot, input);
        await Stock(db).ReviewAsync(owner, depot, draft.Id, "REJECTED", "Cần kiểm đếm lại");
        Assert.Empty(await Inventory(db).GetAsync(owner, depot));
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).ReviewAsync(owner, depot, draft.Id, "POSTED"));
        await Posted(db, input = Input());
        var unknown = Input(100); unknown.CostsConfirmed = false; await Posted(db, unknown);
        var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 1100 });
        Assert.Null((await Stock(db).BatchCostsAsync(owner, depot, batch.Id)).CostOfGoods);
    }
    [Fact]
    public async Task Changed_retry_payload_is_rejected_without_second_receipt()
    {
        await using var db = Open(); var input = Input(); await Stock(db).CreateAsync(owner, depot, input);
        input.PurchaseCost++;
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).CreateAsync(owner, depot, input));
        Assert.Single((await Stock(db).ListAsync(owner, depot, new())).Items);
    }
    [Fact]
    public async Task Concurrent_sales_cannot_oversell_the_same_ten_chairs()
    {
        Guid lineId;
        await using (var db = Open()) lineId = (await Posted(db, Input(10, 300000, true))).Lines.Single().Id;
        async Task<bool> Sell()
        {
            await using var db = Open();
            try { await Stock(db).SellAsync(owner, depot, lineId, new(Guid.NewGuid(), 7, 450000, 0, "Đã giao")); return true; }
            catch (DepotConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Sell(), Sell());
        Assert.Single(outcomes, x => x);
        await using var verify = Open();
        Assert.Equal(3, Assert.Single((await Stock(verify).ReusableAsync(owner, depot, new())).Items).AvailableQuantity);
    }
    [Fact]
    public async Task Void_preserves_receipt_history_but_cannot_remove_reserved_stock()
    {
        await using var db = Open(); var receipt = await Posted(db, Input(100));
        var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 60 });
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).ReviewAsync(owner, depot, receipt.Id, "VOIDED", "Nhập nhầm nguồn"));
        await Batches(db).CancelAsync(owner, depot, batch.Id);
        await Stock(db).ReviewAsync(owner, depot, receipt.Id, "VOIDED", "Nhập nhầm nguồn");
        await Stock(db).ReviewAsync(owner, depot, receipt.Id, "VOIDED", "Nhập nhầm nguồn");
        var voided = Assert.Single((await Stock(db).ListAsync(owner, depot, new())).Items);
        Assert.Equal("VOIDED", voided.Status);
        Assert.Empty(await Inventory(db).GetAsync(owner, depot));
        Assert.NotNull((await db.DepotStockReceipts.SingleAsync(x => x.Id == receipt.Id)).PostedAt);
    }
    [Fact]
    public async Task Concurrent_factory_batches_reserve_the_source_only_once()
    {
        await using (var db = Open()) await Posted(db, Input(100));
        async Task<bool> Create()
        {
            await using var db = Open();
            try { await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 70 }); return true; }
            catch (DepotConflictException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Create(), Create()), x => x);
        await using var verify = Open(); Assert.Equal(30, Assert.Single(await Inventory(verify).GetAsync(owner, depot)).AvailableKg);
    }
    [Fact]
    public async Task Settlement_report_subtracts_source_cost_delivery_and_fee_only_once()
    {
        await using var db = Open();
        var first = Input(); first.ReceivedAt = DateTime.UtcNow.AddHours(-2);
        var second = Input(500, 6000); second.ReceivedAt = DateTime.UtcNow.AddHours(-1);
        await Posted(db, first); await Posted(db, second);
        var batch = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 1300 });
        await Stock(db).AddBatchExpenseAsync(owner, depot, batch.Id, new(Guid.NewGuid(), "Công, xăng xe, bốc dỡ", 600000));
        var stored = await db.InventoryBatches.SingleAsync(x => x.Id == batch.Id);
        stored.Status = "PAID"; stored.SettledAt = DateTime.UtcNow; stored.GrossAmount = 8450000;
        stored.PlatformFeeAmount = 84500; stored.NetAmount = 8365500; await db.SaveChangesAsync();
        Assert.Equal(965500, (await Stock(db).BatchCostsAsync(owner, depot, batch.Id)).PretaxDifference);
    }
    private sealed class NoImages : ICloudinaryService
    {
        public Task<string> UploadImageAsync(Stream stream, string fileName) => throw new NotSupportedException();
        public Task<string> UploadAvatarAsync(Stream stream, string fileName) => throw new NotSupportedException();
        public Task<bool> DeleteImageAsync(string publicId) => throw new NotSupportedException();
    }
}
