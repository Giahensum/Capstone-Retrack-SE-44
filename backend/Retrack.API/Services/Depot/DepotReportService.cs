using Microsoft.EntityFrameworkCore;
using RevenueReportDto = Retrack.API.DTOs.Depot.RevenueReportDto;
using RevenuePointDto = Retrack.API.DTOs.Depot.RevenuePointDto;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class DepotReportService(AppDbContext db, IDepotService scope, IInventoryService inventory) : IDepotReportService
{
    public async Task<RevenueReportDto> RevenueAsync(Guid ownerId, Guid depotId, PeriodQuery period)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        var revenue = await db.BatchQualityChecks.AsNoTracking().Where(q => q.Batch.DepotId == depotId && q.IsAccepted &&
            q.PaymentProofUrl != null && q.CreatedAt >= start && q.CreatedAt < end)
            .GroupBy(q => q.CreatedAt.AddHours(7).Date).Select(g => new { Date = g.Key, Amount = g.Sum(q => q.NetAmount) }).ToListAsync();
        var costs = await (from t in db.PlatformTransactions.AsNoTracking()
            join p in db.PickupRequests on t.SourceId equals p.Id
            where t.SourceType == "PICKUP_REQUEST" && p.TargetDepotId == depotId && t.CreatedAt >= start && t.CreatedAt < end
            group p by t.CreatedAt.AddHours(7).Date into g select new { Date = g.Key, Amount = g.Sum(p => p.NetAmount) }).ToListAsync();
        DateOnly Bucket(DateTime day) => period.GroupBy switch {
            "month" => new(day.Year, day.Month, 1),
            "week" => DateOnly.FromDateTime(day.AddDays(-((int)day.DayOfWeek + 6) % 7)),
            _ => DateOnly.FromDateTime(day)
        };
        var points = revenue.Select(r => new RevenuePointDto(Bucket(r.Date), r.Amount, 0))
            .Concat(costs.Select(r => new RevenuePointDto(Bucket(r.Date), 0, r.Amount)))
            .GroupBy(r => r.Date).OrderBy(g => g.Key).Select(g => new RevenuePointDto(g.Key, g.Sum(r => r.Revenue), g.Sum(r => r.PurchaseCost))).ToList();
        return new(revenue.Sum(r => r.Amount), costs.Sum(r => r.Amount), points);
    }
    public async Task<DashboardDto> DashboardAsync(Guid ownerId, Guid depotId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var day = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
        var report = await RevenueAsync(ownerId, depotId, new());
        return new(await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId && p.CreatedAt >= day),
            (await inventory.GetAsync(ownerId, depotId)).Sum(i => i.AvailableKg),
            await db.InventoryBatches.CountAsync(b => b.DepotId == depotId && b.Status != "COMPLETED" && b.Status != "CANCELLED" && b.Status != "REJECTED" && b.Status != "VERIFIED"),
            await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId && p.Status == "AWAITING_PAYMENT"), report.Revenue, report.PurchaseCost);
    }
    public async Task<PagedResult<StaffPerformanceDto>> PerformanceAsync(Guid ownerId, Guid depotId, PeriodQuery period, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        var source = db.DepotStaffs.AsNoTracking().Where(s => s.DepotId == depotId).Select(s => new { s.Id, s.User.FullName, s.StaffType,
            CompletedCount = s.StaffType == "DRIVER" ? db.TransportJobs.Count(t => t.DriverId == s.UserId && t.Batch.DepotId == depotId && t.Status == "DELIVERED" && t.UpdatedAt >= start && t.UpdatedAt < end)
                : db.PickupRequests.Count(p => p.AcceptedCollectorId == s.UserId && p.TargetDepotId == depotId && p.Status == "DONE" && p.UpdatedAt >= start && p.UpdatedAt < end),
            WeightKg = s.StaffType == "DRIVER" ? db.TransportJobs.Where(t => t.DriverId == s.UserId && t.Batch.DepotId == depotId && t.Status == "DELIVERED" && t.UpdatedAt >= start && t.UpdatedAt < end).Sum(t => t.Batch.DeclaredWeightKg)
                : db.PickupRequestItems.Where(i => i.PickupRequest.AcceptedCollectorId == s.UserId && i.PickupRequest.TargetDepotId == depotId && i.PickupRequest.Status == "DONE" && i.PickupRequest.UpdatedAt >= start && i.PickupRequest.UpdatedAt < end).Sum(i => i.WeightKg),
            Amount = db.PickupRequests.Where(p => p.AcceptedCollectorId == s.UserId && p.TargetDepotId == depotId && p.Status == "DONE" && p.UpdatedAt >= start && p.UpdatedAt < end).Sum(p => p.GrossAmount) });
        return await DepotService.PageAsync(source.OrderByDescending(s => s.CompletedCount).ThenBy(s => s.Id)
            .Select(s => new StaffPerformanceDto(s.Id, s.FullName, s.StaffType, s.CompletedCount, s.WeightKg, s.Amount)), query);
    }
    public async Task<PagedResult<StaffHistoryDto>> HistoryAsync(Guid ownerId, Guid depotId, Guid staffId, PeriodQuery period, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        var staff = await db.DepotStaffs.AsNoTracking().SingleOrDefaultAsync(s => s.Id == staffId && s.DepotId == depotId)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên trong kho.");
        var source = staff.StaffType == "DRIVER"
            ? db.TransportJobs.AsNoTracking().Where(t => t.DriverId == staff.UserId && t.Batch.DepotId == depotId && t.Status == "DELIVERED" && t.UpdatedAt >= start && t.UpdatedAt < end)
                .OrderByDescending(t => t.UpdatedAt).ThenBy(t => t.Id)
                .Select(t => new StaffHistoryDto(t.Id, "TRANSPORT", t.Status, t.Batch.DeclaredWeightKg, 0, t.UpdatedAt))
            : db.PickupRequests.AsNoTracking().Where(p => p.AcceptedCollectorId == staff.UserId && p.TargetDepotId == depotId && p.Status == "DONE" && p.UpdatedAt >= start && p.UpdatedAt < end)
                .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id)
                .Select(p => new StaffHistoryDto(p.Id, "PICKUP", p.Status, p.Items.Sum(i => i.WeightKg), p.GrossAmount, p.UpdatedAt));
        return await DepotService.PageAsync(source, query);
    }
    public async Task<PagedResult<FeeEntryDto>> FeesAsync(Guid ownerId, Guid depotId, PeriodQuery period, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        return await DepotService.PageAsync(FeeRows(depotId).Where(t => t.CreatedAt >= start && t.CreatedAt < end)
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).Select(t => new FeeEntryDto(t.Id, t.SourceId, t.FeeAmount, t.CreatedAt)), query);
    }
    private IQueryable<PlatformTransaction> FeeRows(Guid depotId) => db.PlatformTransactions.AsNoTracking()
        .Where(t => t.SourceType == "PICKUP_REQUEST" && db.PickupRequests.Any(p => p.Id == t.SourceId && p.TargetDepotId == depotId));
    public async Task<FeeSummaryDto> FeeSummaryAsync(Guid ownerId, Guid depotId, PeriodQuery period)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        return new(await FeeRows(depotId).Where(t => t.CreatedAt >= start && t.CreatedAt < end).SumAsync(t => t.FeeAmount),
            await db.PlatformFeeInvoices.Where(i => i.OwnerId == ownerId && i.Status == "UNPAID").SumAsync(i => i.Amount),
            await db.PlatformFeeInvoices.Where(i => i.OwnerId == ownerId && i.Status == "SUBMITTED").SumAsync(i => i.Amount));
    }
    public async Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        return await DepotService.PageAsync(db.PlatformFeeInvoices.AsNoTracking().Where(i => i.OwnerId == ownerId)
            .OrderByDescending(i => i.PeriodStart).ThenBy(i => i.Id).Select(i => new FeeInvoiceDto(i.Id, i.PeriodStart, i.Amount, i.Status, i.PaymentProofUrl, i.SubmittedAt)), query);
    }
    public async Task ConfirmInvoiceAsync(Guid ownerId, Guid depotId, Guid id, PaymentProofDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!Uri.TryCreate(dto.PaymentProofUrl, UriKind.Absolute, out var url) || (url.Scheme != "https" && url.Scheme != "http")) throw new ArgumentException("Chứng từ phải là URL HTTP/HTTPS.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var invoice = await db.PlatformFeeInvoices.FromSqlInterpolated($"SELECT * FROM platform_fee_invoices WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
        if (invoice.OwnerId != ownerId) throw new DepotForbiddenException();
        if (invoice.Status != "UNPAID")
        {
            if (invoice.PaymentProofUrl == dto.PaymentProofUrl) return;
            throw new DepotConflictException("Hóa đơn đã có chứng từ thanh toán.");
        }
        invoice.Status = "SUBMITTED"; invoice.PaymentProofUrl = dto.PaymentProofUrl; invoice.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
