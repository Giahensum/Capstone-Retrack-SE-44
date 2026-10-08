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
        var monthSettled = batches.Where(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= monthStart);
        var sixMonthStart = monthStart.AddMonths(-5);
        var sixMonths = await batches.Where(x => (x.Status == "COMPLETED" || x.Status == "PAID") && x.SettledAt >= sixMonthStart)
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
        var demands = await Db.FactoryDemands.CountAsync(x => x.FactoryId == factory.Id && x.IsActive, ct);
        var partners = await Db.FactoryDepotPartnerships.CountAsync(x => x.FactoryId == factory.Id, ct);
        return ServiceResult<DashboardResponse>.Success(data: new DashboardResponse
        {
            OrderCount = await batches.CountAsync(ct),
            ActiveDemandCount = demands,
            PartnerCount = partners,
            PendingQcCount = await pendingQcQuery.CountAsync(x => x.Status != "VERIFIED", ct),
            PendingSettlementCount = await batches.CountAsync(x => x.Status == "VERIFIED", ct),
            MonthlyPurchasedKg = await monthSettled.SumAsync(x => x.ActualWeightKg ?? 0, ct),
            MonthlyNetPayment = await monthSettled.SumAsync(x => x.NetAmount ?? 0, ct),
            PeriodPurchasedKg = await settled.SumAsync(x => x.ActualWeightKg ?? 0, ct),
            PeriodGrossAmount = await settled.SumAsync(x => x.GrossAmount ?? 0, ct),
            PeriodNetPayment = await settled.SumAsync(x => x.NetAmount ?? 0, ct),
            PeriodFeeAmount = await settled.SumAsync(x => x.PlatformFeeAmount ?? 0, ct),
            MaterialVolumes = materialVolumes,
            SixMonthPayments = sixMonths,
            PriorityOrders = priority.Select(FactoryOrderMapper.Map).ToList(),
            RecentOrders = recent.Select(FactoryOrderMapper.Map).ToList()
        });
    }
}
