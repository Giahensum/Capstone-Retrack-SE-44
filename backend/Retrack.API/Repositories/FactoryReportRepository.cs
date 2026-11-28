using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Factory;

namespace Retrack.API.Repositories;

public sealed class FactoryReportRepository(AppDbContext db, IFactoryOrderReadRepository orders) : IFactoryReportRepository
{
    public async Task<DashboardResponse> DashboardAsync(Guid factoryId, DateTime periodStart, DateTime monthStart, CancellationToken ct)
    {
        var batches = db.InventoryBatches.AsNoTracking().Where(x => x.TargetFactoryId == factoryId && x.Status != "MARKETPLACE" && x.Status != "DRAFT");
        var settled = batches.Where(x => ((x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID") && x.SettledAt >= periodStart);
        var settledStatuses = batches.Where(x => (x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID");
        var summary = await batches.GroupBy(_ => 1)
            .Select(g => new
            {
                OrderCount = g.Count(),
                PendingQcCount = g.Count(x =>
                    x.Status == "RECEIVED" || x.Status == "WEIGHED" || x.Status == "DELIVERED" ||
                    x.Status == "NEGOTIATING" || x.Status == "RETURN_REQUESTED" || x.Status == "RETURNING" ||
                    (x.Status == "REJECTED" && x.QualityCheck != null) ||
                    ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") &&
                     x.TransportJob != null && x.TransportJob.Status == "DELIVERED")),
                PendingSettlementCount = g.Count(x => x.Status == "VERIFIED" || x.Status == "SETTLEMENT_PENDING" ||
                    x.Status == "SETTLEMENT_DISPUTED" || x.Status == "SETTLEMENT_ACCEPTED" ||
                    (x.Status == "COMPLETED" && x.SettlementWorkflowVersion > 0)),
                MonthlyPurchasedKg = g.Sum(x => ((x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID") && x.SettledAt >= monthStart
                    ? x.ActualWeightKg ?? 0 : 0),
                MonthlyNetPayment = g.Sum(x => ((x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID") && x.SettledAt >= monthStart
                    ? x.NetAmount ?? 0 : 0),
                PeriodPurchasedKg = g.Sum(x => ((x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.ActualWeightKg ?? 0 : 0),
                PeriodGrossAmount = g.Sum(x => ((x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.GrossAmount ?? 0 : 0),
                PeriodNetPayment = g.Sum(x => ((x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.NetAmount ?? 0 : 0),
                PeriodFeeAmount = g.Sum(x => ((x.Status == "COMPLETED" && x.SettlementWorkflowVersion == 0) || x.Status == "PAID") && x.SettledAt >= periodStart
                    ? x.PlatformFeeAmount ?? 0 : 0)
            })
            .SingleOrDefaultAsync(ct);
        var counts = await db.Factories.AsNoTracking().Where(x => x.Id == factoryId)
            .Select(x => new
            {
                ActiveDemandCount = db.FactoryDemands.Count(d => d.FactoryId == x.Id && d.IsActive),
                PartnerCount = db.FactoryDepotPartnerships.Count(p => p.FactoryId == x.Id)
            })
            .SingleAsync(ct);
        var sixMonthStart = monthStart.AddMonths(-5);
        var sixMonths = await settledStatuses.Where(x => x.SettledAt >= sixMonthStart)
            .GroupBy(x => new { x.SettledAt!.Value.Year, x.SettledAt!.Value.Month })
            .Select(g => new MonthlyPaymentResponse(g.Key.Year, g.Key.Month, g.Sum(x => x.NetAmount ?? 0)))
            .ToListAsync(ct);
        var materialVolumes = await settled.GroupBy(x => x.MaterialType)
            .Select(g => new MaterialVolumeResponse(g.Key, g.Sum(x => x.ActualWeightKg ?? 0))).ToListAsync(ct);
        var priority = await orders.DashboardPriorityAsync(factoryId, ct);
        var recent = await orders.DashboardRecentAsync(factoryId, ct);
        return new DashboardResponse
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
        };
    }
}
