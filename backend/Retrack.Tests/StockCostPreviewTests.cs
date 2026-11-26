using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    [Fact]
    public async Task Single_output_preview_without_reference_matches_persisted_actual_stock_cost()
    {
        await using var db = Open();
        var input = Input(90); input.PurchaseCost = 300000; input.Lines[0].ReferencePrice = 0;
        input.Expenses = [new("OTHER", "Chi phí đưa hàng về và chuẩn bị hàng", 50000)];
        input.PostImmediately = true;
        var preview = await Stock(db).PreviewCostsAsync(owner, depot, new(input.PurchaseCost, input.Lines, input.Expenses));
        var line = Assert.Single(preview.Lines);
        Assert.Equal(350000, preview.TotalCost); Assert.Equal(100, line.SharePercent);
        Assert.Equal(0, preview.ReferenceTotal); Assert.Equal(350000m / 90m, line.UnitCost);
        Assert.False(await db.DepotStockReceipts.AnyAsync(x => x.DepotId == depot));
        var receipt = await Stock(db).CreateAsync(owner, depot, input);
        Assert.Equal(line.AllocatedCost, Assert.Single(receipt.Lines).AllocatedCost);
        Assert.Equal(receipt.Id, (await Stock(db).CreateAsync(owner, depot, input)).Id);
        await using var reload = Open();
        Assert.Equal(90, Assert.Single(await Inventory(reload).GetAsync(owner, depot)).AvailableKg);
        Assert.Equal(350000, (await reload.DepotStockLines.SingleAsync(x => x.ReceiptId == receipt.Id)).AllocatedCost);
    }

    [Fact]
    public async Task Preview_explains_actual_auction_example_and_never_persists_inventory()
    {
        await using var db = Open();
        var result = await Stock(db).PreviewCostsAsync(owner, depot, new(11000000,
            [new() { Name = "Sắt", Quantity = 1000, ReferencePrice = 9994, MaterialType = "IRON" },
             new() { Kind = "REUSABLE", Name = "Tivi", Quantity = 5, ReferencePrice = 400000, Unit = "cái" }],
            [new("FUEL", "Xăng xe", 200000), new("LABOR", "Phân loại", 500000)]));
        Assert.Equal(11700000, result.TotalCost); Assert.Equal(11994000, result.ReferenceTotal);
        Assert.Equal(9749025, result.Lines[0].AllocatedCost);
        Assert.Equal(1950975, result.Lines[1].AllocatedCost); Assert.Equal(390195, result.Lines[1].UnitCost);
        Assert.Equal(100, result.Lines.Sum(x => x.SharePercent));
        Assert.False(await db.DepotStockReceipts.AnyAsync(x => x.DepotId == depot));
    }
    [Fact]
    public async Task Preview_rejects_staff_and_invalid_reference_values_and_scales_to_many_different_goods()
    {
        await using var db = Open();
        var input = new StockCostPreviewInput(11000000,
            Enumerable.Range(1, 30).Select(i => new StockLineInput { Name = "Hàng " + i, Quantity = i, ReferencePrice = 400000, MaterialType = "IRON", DirectCost = i * 1000 }).ToList(), []);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).PreviewCostsAsync(employee, depot, input));
        var result = await Stock(db).PreviewCostsAsync(owner, depot, input);
        Assert.Equal(11465000, result.TotalCost); Assert.Equal(result.TotalCost, result.Lines.Sum(x => x.AllocatedCost));
        input.Lines[0].ReferencePrice = 0;
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).PreviewCostsAsync(owner, depot, input));
    }
}
