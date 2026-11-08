using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotStockService
{
    public async Task<StockCostPreviewDto> PreviewCostsAsync(Guid ownerId, Guid depotId, StockCostPreviewInput dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        ValidateReceipt(new() { OperationId = Guid.NewGuid(), Reference = "Xem trước phân bổ", ReceivedAt = DateTime.UtcNow,
            PurchaseCost = dto.PurchaseCost, CostsConfirmed = true, Lines = dto.Lines, Expenses = dto.Expenses }, true);
        var common = dto.Expenses.Sum(x => x.Amount);
        var costs = DepotCosting.Allocate(dto.PurchaseCost, common,
            dto.Lines.Select(x => new CostingLine(x.Quantity, x.ReferencePrice, x.DirectCost)).ToArray());
        var reference = dto.Lines.Sum(x => x.Quantity * x.ReferencePrice);
        return new(dto.PurchaseCost, common, dto.Lines.Sum(x => x.DirectCost), costs.Sum(), reference,
            dto.Lines.Select((x, i) => new StockCostPreviewLine(x.Name, x.Unit, x.Quantity, x.ReferencePrice,
                x.Quantity * x.ReferencePrice, 100 * x.Quantity * x.ReferencePrice / reference,
                costs[i] - x.DirectCost, x.DirectCost, costs[i], costs[i] / x.Quantity)).ToList());
    }
}
