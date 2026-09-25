using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotService(AppDbContext db) : IDepotService
{
    public async Task<List<DepotSummaryDto>> GetDepotsAsync(Guid ownerId)
    {
        await RequireActiveOwnerAsync(ownerId);
        return await db.Depots.AsNoTracking().Where(d => d.OwnerId == ownerId)
            .OrderBy(d => d.Name).ThenBy(d => d.Id).Select(d => new DepotSummaryDto(d.Id, d.Name)).ToListAsync();
    }

    private async Task RequireActiveOwnerAsync(Guid ownerId)
    {
        if (!await db.Users.AnyAsync(u => u.Id == ownerId && u.IsActive && u.Role == "DEPOT_OWNER"))
            throw new DepotForbiddenException();
    }

    public async Task RequireOwnerAsync(Guid ownerId, Guid depotId)
    {
        await RequireActiveOwnerAsync(ownerId);
        if (!await db.Depots.AnyAsync(d => d.Id == depotId && d.OwnerId == ownerId))
            throw new DepotForbiddenException();
    }

    private IQueryable<PickupRequest> Payments(Guid depotId) => db.PickupRequests.AsNoTracking()
        .Where(p => p.TargetDepotId == depotId &&
            (p.Status == "AWAITING_PAYMENT" || p.Status == "PAYMENT_SENT" || p.Status == "DONE"));

    public async Task<PagedResult<DepotPaymentDto>> GetPaymentsAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100)
            throw new ArgumentException("Phân trang không hợp lệ.");
        var source = Payments(depotId);
        if (!string.IsNullOrEmpty(query.Status))
        {
            if (query.Status is not ("AWAITING_PAYMENT" or "PAYMENT_SENT" or "DONE"))
                throw new ArgumentException("Trạng thái thanh toán không hợp lệ.");
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

    public async Task<DepotPaymentDto> GetPaymentAsync(Guid ownerId, Guid depotId, Guid requestId)
    {
        await RequireOwnerAsync(ownerId, depotId);
        return await Project(Payments(depotId).Where(p => p.Id == requestId)).SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy thanh toán trong kho.");
    }

    public async Task<PaymentSummaryDto> GetPaymentSummaryAsync(Guid ownerId, Guid depotId)
    {
        await RequireOwnerAsync(ownerId, depotId);
        var pending = Payments(depotId).Where(p => p.Status == "AWAITING_PAYMENT");
        // Ngày nghiệp vụ theo giờ Việt Nam, đổi sang UTC để so sánh với timestamptz.
        var start = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
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
