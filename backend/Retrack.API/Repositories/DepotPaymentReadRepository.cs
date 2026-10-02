using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;
public sealed class DepotPaymentReadRepository(AppDbContext db) : IDepotPaymentReadRepository
{
    private IQueryable<PickupRequest> Payments(Guid depotId) => db.PickupRequests.AsNoTracking()
        .Where(p => p.TargetDepotId == depotId &&
            (p.Status == "AWAITING_PAYMENT" || p.Status == "PAYMENT_SENT" || p.Status == "DONE"));

    public async Task<PagedResult<DepotPaymentDto>> ListAsync(Guid depotId, DepotQuery query)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100)
            throw new ArgumentException("Phân trang không hợp lệ.");
        var source = Payments(depotId);
        if (!string.IsNullOrEmpty(query.Status))
        {
            source = source.Where(p => p.Status == query.Status);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(p => p.Seller.FullName.Contains(search) || p.Id.ToString().Contains(search));
        }
        var count = await source.CountAsync();
        source = query.Sort switch
        {
            "oldest" => source.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            "amount" => source.OrderByDescending(p => p.NetAmount).ThenBy(p => p.Id),
            "newest" => source.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            _ => throw new ArgumentException("Thứ tự không hợp lệ.")
        };
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new PagedResult<DepotPaymentDto>
        {
            Items = offset > int.MaxValue ? [] : await Project(source.Skip((int)offset).Take(query.PageSize)).ToListAsync(),
            TotalCount = count, Page = query.Page, PageSize = query.PageSize
        };
    }

    public async Task<DepotPaymentDto?> FindAsync(Guid depotId, Guid requestId)
    {
        return await Project(Payments(depotId).Where(p => p.Id == requestId)).SingleOrDefaultAsync();
    }

    public async Task<PaymentSummaryDto> SummaryAsync(Guid depotId, DateTime start)
    {
        var pending = Payments(depotId).Where(p => p.Status == "AWAITING_PAYMENT");
        var sent = Payments(depotId).Where(p => p.Status != "AWAITING_PAYMENT" &&
            db.PlatformTransactions.Any(t => t.SourceType == "PICKUP_REQUEST" && t.SourceId == p.Id && t.CreatedAt >= start));
        return new(await pending.CountAsync(), await pending.SumAsync(p => p.NetAmount),
            await sent.CountAsync(), await sent.SumAsync(p => p.NetAmount));
    }

    private static IQueryable<DepotPaymentDto> Project(IQueryable<PickupRequest> source) => source.Select(p =>
        new DepotPaymentDto(p.Id, p.TargetDepotId!.Value, p.Seller.FullName, p.Seller.Phone,
            p.AcceptedCollector == null ? null : p.AcceptedCollector.FullName,
            p.Address, p.Status, p.GrossAmount, p.PlatformFeePercentage, p.PlatformFeeAmount, p.NetAmount,
            p.PaymentProofUrl, p.CheckinImageUrl, p.CreatedAt,
            p.Items.OrderBy(i => i.Id).Select(i => new PaymentItemDto(i.MaterialType, i.WeightKg, i.PricePerKg, i.SubTotal)).ToList()));
}
