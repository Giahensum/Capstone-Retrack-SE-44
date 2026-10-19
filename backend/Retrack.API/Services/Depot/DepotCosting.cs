namespace Retrack.API.Services.Depot;

public record CostingLine(decimal Quantity, decimal ReferencePrice, decimal DirectCost);
public record CostingSource(Guid Id, decimal Quantity, decimal? UnitCost);
public record CostingTake(Guid Id, decimal Quantity, decimal? UnitCost);

public static class DepotCosting
{
    public static decimal[] Allocate(decimal purchase, decimal common, IReadOnlyList<CostingLine> lines)
    {
        if (purchase < 0 || common < 0 || lines.Count == 0 ||
            lines.Any(x => x.Quantity <= 0 || x.ReferencePrice <= 0 || x.DirectCost < 0))
            throw new ArgumentException("Số lượng, giá tham khảo và chi phí phân bổ không hợp lệ.");
        var totalValue = lines.Sum(x => x.Quantity * x.ReferencePrice);
        var pool = purchase + common;
        var remaining = pool;
        return lines.Select((x, index) => {
            var share = index == lines.Count - 1 ? remaining : Math.Min(remaining,
                decimal.Round(pool * (x.Quantity * x.ReferencePrice / totalValue), 0, MidpointRounding.AwayFromZero));
            remaining -= share;
            return share + x.DirectCost;
        }).ToArray();
    }

    // Danh sách nguồn đã được sắp theo thời gian nhận; không suy ra nguồn vật lý khi hàng trộn.
    public static List<CostingTake> Take(decimal quantity, IReadOnlyList<CostingSource> sources)
    {
        if (quantity <= 0) throw new ArgumentException("Khối lượng phải dương.");
        var result = new List<CostingTake>();
        foreach (var source in sources.Where(x => x.Quantity > 0))
        {
            var take = Math.Min(quantity, source.Quantity);
            result.Add(new(source.Id, take, source.UnitCost));
            quantity -= take;
            if (quantity == 0) return result;
        }
        throw new DepotConflictException("Nguồn tồn kho khả dụng không đủ để phân bổ.");
    }
}
