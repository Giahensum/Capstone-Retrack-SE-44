namespace Retrack.API.Services.Depot;

public record CostingLine(decimal Quantity, decimal ReferencePrice, decimal DirectCost);
public record CostingItem(Guid Id, decimal Amount);
public record ItemCostingLine(Guid ItemId, CostingLine Cost);
public record CostingSource(Guid Id, decimal Quantity, decimal? UnitCost);
public record CostingTake(Guid Id, decimal Quantity, decimal? UnitCost);

public static class DepotCosting
{
    public static decimal[] Allocate(decimal purchase, decimal common, IReadOnlyList<CostingLine> lines)
    {
        if (purchase < 0 || common < 0 || lines.Count == 0 ||
            lines.Any(x => x.Quantity <= 0 || x.ReferencePrice < 0 || lines.Count > 1 && x.ReferencePrice == 0 || x.DirectCost < 0))
            throw new ArgumentException("Số lượng, giá tham khảo và chi phí phân bổ không hợp lệ.");
        // Một đầu ra nhận toàn bộ vốn, không cần giá bán tham khảo để tạo tỷ trọng.
        if (lines.Count == 1) return [purchase + common + lines[0].DirectCost];
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

    public static decimal[] AllocateItems(decimal common, IReadOnlyList<CostingItem> items, IReadOnlyList<ItemCostingLine> lines)
    {
        if(common<0 || items.Count==0 || lines.Count==0 || items.Any(x=>x.Id==Guid.Empty || x.Amount<=0) ||
            items.Select(x=>x.Id).Distinct().Count()!=items.Count || lines.Any(x=>!items.Any(i=>i.Id==x.ItemId)) ||
            items.Any(x=>!lines.Any(l=>l.ItemId==x.Id)))
            throw new ArgumentException("Dòng hàng phải giữ đủ nguồn hạng mục và tiền mua hợp lệ.");
        // Chỉ chi phí chung được chia giữa các hạng mục; tiền mua của mỗi hạng mục giữ nguyên.
        var commonShares=Allocate(0,common,items.Select(x=>new CostingLine(1,x.Amount,0)).ToArray());
        var result=new decimal[lines.Count];
        for(var i=0;i<items.Count;i++)
        {
            var indices=Enumerable.Range(0,lines.Count).Where(j=>lines[j].ItemId==items[i].Id).ToArray();
            var costs=Allocate(items[i].Amount,commonShares[i],indices.Select(j=>lines[j].Cost).ToArray());
            for(var j=0;j<indices.Length;j++)result[indices[j]]=costs[j];
        }
        return result;
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
