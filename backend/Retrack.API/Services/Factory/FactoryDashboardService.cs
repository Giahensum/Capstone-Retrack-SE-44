using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public class FactoryDashboardService(AppDbContext db) : FactoryServiceBase(db), IFactoryDashboardService
{
    public async Task<ServiceResult<DashboardResponse>> GetAsync(Guid userId, CancellationToken ct, string period = "month")
    {
        if (period is not ("day" or "month" or "year"))
            return ServiceResult<DashboardResponse>.Invalid("Kỳ thống kê phải là ngày, tháng hoặc năm.");
        var factory = await CurrentFactory(userId, ct);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodStart = period switch
        {
            "day" => now.Date,
            "year" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => monthStart
        };
        var batches = Db.InventoryBatches.AsNoTracking().Where(x => x.TargetFactoryId == factory.Id && x.Status != "MARKETPLACE" && x.Status != "DRAFT");
        var settled = batches.Where(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= periodStart);
        var settledStatuses = batches.Where(x => x.Status == "COMPLETED" || x.Status == "PAID");
        var summary = await batches.GroupBy(_ => 1)
            .Select(g => new
            {
                OrderCount = g.Count(),
                PendingQcCount = g.Count(x =>
                    x.Status == "RECEIVED" || x.Status == "WEIGHED" || x.Status == "DELIVERED" ||
                    ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") &&
                     x.TransportJob != null && x.TransportJob.Status == "DELIVERED")),
                PendingSettlementCount = g.Count(x => x.Status == "VERIFIED"),
                MonthlyPurchasedKg = g.Sum(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= monthStart
                    ? x.ActualWeightKg ?? 0 : 0),
                MonthlyNetPayment = g.Sum(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= monthStart
                    ? x.NetAmount ?? 0 : 0),
                PeriodPurchasedKg = g.Sum(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.ActualWeightKg ?? 0 : 0),
                PeriodGrossAmount = g.Sum(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.GrossAmount ?? 0 : 0),
                PeriodNetPayment = g.Sum(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.NetAmount ?? 0 : 0),
                PeriodFeeAmount = g.Sum(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.PlatformFeeAmount ?? 0 : 0)
            })
            .SingleOrDefaultAsync(ct);
        var counts = await Db.Factories.AsNoTracking().Where(x => x.Id == factory.Id)
            .Select(x => new
            {
                ActiveDemandCount = Db.FactoryDemands.Count(d => d.FactoryId == x.Id && d.IsActive),
                PartnerCount = Db.FactoryDepotPartnerships.Count(p => p.FactoryId == x.Id)
            })
            .SingleAsync(ct);
        var sixMonthStart = monthStart.AddMonths(-5);
        var sixMonths = await settledStatuses.Where(x => x.SettledAt >= sixMonthStart)
            .GroupBy(x => new { x.SettledAt!.Value.Year, x.SettledAt!.Value.Month })
            .Select(g => new MonthlyPaymentResponse(g.Key.Year, g.Key.Month, g.Sum(x => x.NetAmount ?? 0)))
            .ToListAsync(ct);
        var materialVolumes = await settled.GroupBy(x => x.MaterialType)
            .Select(g => new MaterialVolumeResponse(g.Key, g.Sum(x => x.ActualWeightKg ?? 0))).ToListAsync(ct);
        var pendingQcQuery = FactoryOrderQueries.ForFactory(Db, factory.Id)
            .Where(x => x.Status == "RECEIVED" || x.Status == "WEIGHED" || x.Status == "DELIVERED" || x.Status == "VERIFIED" ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") && x.TransportJob != null && x.TransportJob.Status == "DELIVERED"));
        var priority = await pendingQcQuery.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(10).ToListAsync(ct);
        var recent = await FactoryOrderQueries.ForFactory(Db, factory.Id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(5).ToListAsync(ct);
        return ServiceResult<DashboardResponse>.Success(data: new DashboardResponse
        {
            OrderCount = summary?.OrderCount ?? 0,
            ActiveDemandCount = counts.ActiveDemandCount,
            PartnerCount = counts.PartnerCount,
            PendingQcCount = summary?.PendingQcCount ?? 0,
            PendingSettlementCount = summary?.PendingSettlementCount ?? 0,
            MonthlyPurchasedKg = summary?.MonthlyPurchasedKg ?? 0,
            MonthlyNetPayment = summary?.MonthlyNetPayment ?? 0,
            PeriodPurchasedKg = summary?.PeriodPurchasedKg ?? 0,
            PeriodGrossAmount = summary?.PeriodGrossAmount ?? 0,
            PeriodNetPayment = summary?.PeriodNetPayment ?? 0,
            PeriodFeeAmount = summary?.PeriodFeeAmount ?? 0,
            MaterialVolumes = materialVolumes,
            SixMonthPayments = sixMonths,
            PriorityOrders = priority.Select(FactoryOrderMapper.Map).ToList(),
            RecentOrders = recent.Select(FactoryOrderMapper.Map).ToList()
        });
    }
}
