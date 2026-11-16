using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    [Theory]
    [InlineData(1000000, false)]
    [InlineData(1000000, true)]
    [InlineData(0, false)]
    public async Task Reports_exclude_declared_output_vat_and_keep_settlement_money(decimal vat, bool priceIncludesVat)
    {
        await using var db = Open();
        await Posted(db, Input(100, 80000));
        var batch = await Batches(db).CreateAsync(owner, depot, new CreateDepotBatchDto
        {
            OperationId = Guid.NewGuid(), MaterialType = "IRON", WeightKg = 100
        });
        var entity = await db.InventoryBatches.SingleAsync(x => x.Id == batch.Id);
        entity.Status = "COMPLETED";
        entity.SettledAt = DateTime.UtcNow;
        entity.GrossAmount = 10000000;
        entity.PlatformFeeAmount = 500000;
        entity.VatTreatment = vat > 0 ? "TAXABLE" : "NOT_APPLICABLE";
        entity.VatPercentage = vat > 0 ? 10 : 0;
        entity.VatAmount = vat;
        entity.InvoiceTotalAmount = 10000000 + vat;
        entity.NetAmount = 9500000 + vat;
        entity.PriceIncludesVat = priceIncludesVat;
        await db.SaveChangesAsync();

        var costs = await Stock(db).BatchCostsAsync(owner, depot, batch.Id, 1, 1);
        Assert.Equal(1500000, costs.PretaxDifference);
        Assert.Equal(vat, costs.OutputVat);
        Assert.Equal(9500000 + vat, costs.SettlementAmount);

        var report = await Reports(db).CostReportAsync(owner, depot, new());
        Assert.Equal(0, report.IncompleteCount);
        Assert.Equal(9500000, report.NetRevenue);
        Assert.Equal(1500000, report.PretaxDifference);
        Assert.Equal(vat, report.OutputVat);
        Assert.Equal(9500000 + vat, report.SettlementAmount);
        var channel = report.Channels.Single(x => x.Channel == "FACTORY");
        Assert.Equal(vat, channel.OutputVat);
        Assert.Equal(report.SettlementAmount, channel.SettlementAmount);

        var detail = await Batches(db).DetailAsync(owner, depot, batch.Id);
        Assert.Equal(priceIncludesVat, detail.Settlement!.PriceIncludesVat);
        Assert.Equal(entity.NetAmount, (await db.InventoryBatches.AsNoTracking().SingleAsync(x => x.Id == batch.Id)).NetAmount);
    }
}
