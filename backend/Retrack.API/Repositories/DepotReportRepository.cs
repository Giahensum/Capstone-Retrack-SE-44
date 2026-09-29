using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;

public sealed class DepotReportRepository(AppDbContext db) : IDepotReportRepository
{
    public async Task<PlatformInvoice?> LockInvoiceAsync(Guid id)
    {
        var invoice = await db.PlatformInvoices.FromSqlInterpolated($"SELECT * FROM platform_invoices WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (invoice != null) await db.Entry(invoice).ReloadAsync();
        return invoice;
    }
    public async Task<(List<DepotDailyAmount> Revenue, List<DepotDailyAmount> Costs)> DailyAmountsAsync(Guid depotId, DateTime start, DateTime end)
    {
        var settled = db.InventoryBatches.AsNoTracking().Where(b => b.DepotId == depotId && b.SettledAt >= start && b.SettledAt < end &&
            (b.Status == "COMPLETED" || b.Status == "PAID") && b.NetAmount != null)
            .Select(b => new { Date = b.SettledAt!.Value, Amount = b.NetAmount!.Value });
        // Giữ dữ liệu QC cũ chưa có SettledAt, không cộng trùng lô đã quyết toán theo contract mới.
        var legacy = db.BatchQualityChecks.AsNoTracking().Where(q => q.Batch.DepotId == depotId && q.Batch.SettledAt == null && q.IsAccepted &&
            q.PaymentProofUrl != null && q.CreatedAt >= start && q.CreatedAt < end)
            .Select(q => new { Date = q.CreatedAt, Amount = q.NetAmount });
        var revenue = await settled.Concat(legacy).GroupBy(q => q.Date.AddHours(7).Date)
            .Select(g => new DepotDailyAmount(g.Key, g.Sum(q => q.Amount))).ToListAsync();
        var costs = await (from t in db.PlatformTransactions.AsNoTracking()
            join p in db.PickupRequests on t.SourceId equals p.Id
            where t.SourceType == "PICKUP_REQUEST" && p.TargetDepotId == depotId && t.CreatedAt >= start && t.CreatedAt < end
            group p by t.CreatedAt.AddHours(7).Date into g select new DepotDailyAmount(g.Key, g.Sum(p => p.NetAmount))).ToListAsync();
        return (revenue, costs);
    }
    public async Task<DepotDashboardCounts> DashboardCountsAsync(Guid depotId, DateTime day) => new(
        await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId && p.CreatedAt >= day),
        await db.InventoryBatches.CountAsync(b => b.DepotId == depotId && b.Status != "COMPLETED" && b.Status != "CANCELLED" && b.Status != "REJECTED" && b.Status != "VERIFIED"),
        await db.PickupRequests.CountAsync(p => p.TargetDepotId == depotId && p.Status == "AWAITING_PAYMENT"));
    public async Task<PagedResult<StaffPerformanceDto>> PerformanceAsync(Guid depotId, DateTime start, DateTime end, DepotQuery query)
    {
        var source = db.DepotStaffs.AsNoTracking().Where(s => s.DepotId == depotId).Select(s => new { s.Id, s.User.FullName, s.StaffType,
            CompletedCount = s.StaffType == "DRIVER" ? db.TransportJobs.Count(t => t.DriverId == s.UserId && t.Batch.DepotId == depotId && t.Status == "DELIVERED" && t.UpdatedAt >= start && t.UpdatedAt < end)
                : db.PickupRequests.Count(p => p.AcceptedCollectorId == s.UserId && p.TargetDepotId == depotId && p.Status == "DONE" && p.UpdatedAt >= start && p.UpdatedAt < end),
            WeightKg = s.StaffType == "DRIVER" ? db.TransportJobs.Where(t => t.DriverId == s.UserId && t.Batch.DepotId == depotId && t.Status == "DELIVERED" && t.UpdatedAt >= start && t.UpdatedAt < end).Sum(t => t.Batch.DeclaredWeightKg)
                : db.PickupRequestItems.Where(i => i.PickupRequest.AcceptedCollectorId == s.UserId && i.PickupRequest.TargetDepotId == depotId && i.PickupRequest.Status == "DONE" && i.PickupRequest.UpdatedAt >= start && i.PickupRequest.UpdatedAt < end).Sum(i => i.WeightKg),
            Amount = db.PickupRequests.Where(p => p.AcceptedCollectorId == s.UserId && p.TargetDepotId == depotId && p.Status == "DONE" && p.UpdatedAt >= start && p.UpdatedAt < end).Sum(p => p.GrossAmount) });
        return await DepotRepositoryPage.ReadAsync(source.OrderByDescending(s => s.CompletedCount).ThenBy(s => s.Id)
            .Select(s => new StaffPerformanceDto(s.Id, s.FullName, s.StaffType, s.CompletedCount, s.WeightKg, s.Amount)), query);
    }
    public async Task<PagedResult<StaffHistoryDto>> HistoryAsync(Guid depotId, DepotStaff staff, DateTime start, DateTime end, DepotQuery query)
    {
        var source = staff.StaffType == "DRIVER"
            ? db.TransportJobs.AsNoTracking().Where(t => t.DriverId == staff.UserId && t.Batch.DepotId == depotId && t.Status == "DELIVERED" && t.UpdatedAt >= start && t.UpdatedAt < end)
                .OrderByDescending(t => t.UpdatedAt).ThenBy(t => t.Id)
                .Select(t => new StaffHistoryDto(t.Id, "TRANSPORT", t.Status, t.Batch.DeclaredWeightKg, 0, t.UpdatedAt))
            : db.PickupRequests.AsNoTracking().Where(p => p.AcceptedCollectorId == staff.UserId && p.TargetDepotId == depotId && p.Status == "DONE" && p.UpdatedAt >= start && p.UpdatedAt < end)
                .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id)
                .Select(p => new StaffHistoryDto(p.Id, "PICKUP", p.Status, p.Items.Sum(i => i.WeightKg), p.GrossAmount, p.UpdatedAt));
        return await DepotRepositoryPage.ReadAsync(source, query);
    }
    public async Task<PagedResult<FeeEntryDto>> FeesAsync(Guid depotId, DateTime start, DateTime end, DepotQuery query)
    {
        return await DepotRepositoryPage.ReadAsync(FeeRows(depotId).Where(t => t.CreatedAt >= start && t.CreatedAt < end)
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).Select(t => new FeeEntryDto(t.Id, t.SourceId, t.FeeAmount, t.CreatedAt)), query);
    }
    private IQueryable<PlatformTransaction> FeeRows(Guid depotId) => db.PlatformTransactions.AsNoTracking()
        .Where(t => t.SourceType == "PICKUP_REQUEST" && db.PickupRequests.Any(p => p.Id == t.SourceId && p.TargetDepotId == depotId));
    public async Task<FeeSummaryDto> FeeSummaryAsync(Guid ownerId, Guid depotId, DateTime start, DateTime end)
    {
        return new(await FeeRows(depotId).Where(t => t.CreatedAt >= start && t.CreatedAt < end).SumAsync(t => t.FeeAmount),
            await db.PlatformInvoices.Where(i => i.PayerId == ownerId && i.Status == "PENDING").SumAsync(i => i.TotalFeeAmount),
            await db.PlatformInvoices.Where(i => i.PayerId == ownerId && i.Status == "SUBMITTED").SumAsync(i => i.TotalFeeAmount));
    }
    public async Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid ownerId, DepotQuery query)
    {
        return await DepotRepositoryPage.ReadAsync(db.PlatformInvoices.AsNoTracking().Where(i => i.PayerId == ownerId)
            .OrderByDescending(i => i.PeriodYear).ThenByDescending(i => i.PeriodMonth).ThenBy(i => i.Id)
            .Select(i => new FeeInvoiceDto(i.Id, new DateOnly(i.PeriodYear, i.PeriodMonth, 1), i.TotalFeeAmount,
                i.Status == "PENDING" ? "UNPAID" : i.Status, i.PaymentProofUrl, i.SubmittedAt)), query);
    }
}
