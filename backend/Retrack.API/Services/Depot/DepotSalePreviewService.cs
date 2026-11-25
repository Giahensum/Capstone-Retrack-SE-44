using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotStockService
{
    public async Task<BatchSalePreviewDto> PreviewSaleAsync(Guid ownerId, Guid depotId, BatchSalePreviewInput dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var material = MaterialCatalog.RequireCode(dto.MaterialType);
        if (dto.WeightKg <= 0 || dto.WeightKg > 1_000_000_000 || decimal.Round(dto.WeightKg, 3) != dto.WeightKg ||
            dto.PricePerKg is < 0 or > 1_000_000_000 || dto.PricePerKg.HasValue && decimal.Round(dto.PricePerKg.Value, 2) != dto.PricePerKg)
            throw new ArgumentException("Cần khối lượng dương tối đa 3 số lẻ và giá dự kiến không âm tối đa 2 số lẻ.");
        Money(dto.SellingCost);
        var layers = await repository.AvailableLayersAsync(depotId, material);
        // Cùng nguồn FIFO và làm tròn snapshot như lúc giữ hàng, nhưng xem trước không ghi/giữ tồn.
        var takes = DepotCosting.Take(dto.WeightKg, layers.Select(x => new CostingSource(x.Id, x.Quantity, x.UnitCost == null ? null : decimal.Round(x.UnitCost.Value, 9))).ToArray());
        var complete = takes.All(x => x.UnitCost.HasValue);
        decimal? cost = complete ? decimal.Round(takes.Sum(x => x.Quantity * x.UnitCost!.Value), 0) : null;
        decimal? revenue = dto.PricePerKg.HasValue ? decimal.Round(dto.WeightKg * dto.PricePerKg.Value, 0, MidpointRounding.AwayFromZero) : null;
        if (revenue > 1_000_000_000_000) throw new ArgumentException("Giá trị dự kiến vượt mức 1.000 tỷ đồng; kiểm tra giá và khối lượng.");
        return new(dto.WeightKg, complete, cost, dto.SellingCost, revenue, revenue - cost - dto.SellingCost,
            complete ? decimal.Ceiling((cost!.Value + dto.SellingCost) / dto.WeightKg * 100) / 100 : null, takes.Count);
    }
}
