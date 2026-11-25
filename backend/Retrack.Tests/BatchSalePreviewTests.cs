using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    [Fact]
    public async Task Sale_preview_uses_exact_fifo_cost_for_mixed_sources_without_reserving_stock()
    {
        await using var db = Open();
        await Stock(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), Reference = "Lô 11 triệu + 700 nghìn", ReceivedAt = DateTime.UtcNow.AddDays(-2),
            PurchaseCost = 11000000, CostsConfirmed = true, PostImmediately = true,
            Lines = [new() { Name = "Sắt", MaterialType = "IRON", Quantity = 1000, ReferencePrice = 9994 }, new() { Kind = "REUSABLE", Name = "Tivi", Unit = "cái", Quantity = 5, ReferencePrice = 400000 }],
            Expenses = [new("FUEL", "Xăng xe", 200000), new("LABOR", "Phân loại", 500000)] });
        await Stock(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), Reference = "Nguồn thứ hai", ReceivedAt = DateTime.UtcNow.AddDays(-1), PurchaseCost = 3000000,
            CostsConfirmed = true, PostImmediately = true, Lines = [new() { Name = "Sắt", MaterialType = "IRON", Quantity = 500, ReferencePrice = 6000 }] });
        var preview = await Stock(db).PreviewSaleAsync(owner, depot, new("IRON", 1300, 10000, 200000));
        Assert.True(preview.HasCompleteCost); Assert.Equal(11549025, preview.CostOfGoods);
        Assert.Equal(13000000, preview.Revenue); Assert.Equal(1250975, preview.EstimatedDifference); Assert.Equal(9037.72m, preview.BreakEvenPricePerKg);
        Assert.Equal(2, preview.SourceCount); Assert.Empty(await db.DepotBatchCostSources.Where(x => x.Batch.DepotId == depot).ToListAsync());
        var created = await Batches(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 1300 });
        var costs = await Stock(db).BatchCostsAsync(owner, depot, created.Id);
        Assert.Equal(preview.CostOfGoods, costs.CostOfGoods);
        Assert.Empty(await db.DepotReuseSales.Where(x => x.Line.Receipt.DepotId == depot).ToListAsync());
    }
    [Fact]
    public async Task Sale_preview_never_reports_unknown_cost_as_zero_profit_and_rejects_overstock_or_other_user()
    {
        await using var db = Open();
        await Stock(db).CreateAsync(owner, depot, new() { OperationId = Guid.NewGuid(), Reference = "Chưa phân bổ vốn", ReceivedAt = DateTime.UtcNow.AddDays(-1), PostImmediately = true,
            Lines = [new() { Name = "Sắt", MaterialType = "IRON", Quantity = 100 }] });
        var preview = await Stock(db).PreviewSaleAsync(owner, depot, new("IRON", 10, 10000));
        Assert.False(preview.HasCompleteCost); Assert.Null(preview.CostOfGoods); Assert.Null(preview.EstimatedDifference); Assert.Null(preview.BreakEvenPricePerKg);
        Assert.Equal(100000, preview.Revenue);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).PreviewSaleAsync(employee, depot, new("IRON", 10, 10000)));
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).PreviewSaleAsync(owner, depot, new("IRON", 101, 10000)));
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).PreviewSaleAsync(owner, depot, new("IRON", 0, 10000)));
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).PreviewSaleAsync(owner, depot, new("IRON", 10, -1)));
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).PreviewSaleAsync(owner, depot, new("IRON", 10, 10000, -1)));
    }
}
