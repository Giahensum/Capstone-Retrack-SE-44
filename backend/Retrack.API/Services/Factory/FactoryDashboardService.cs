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
    public async Task<ServiceResult<DashboardResponse>> GetAsync(Guid userId, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var batches = await FactoryOrderQueries.ForFactory(Db, factory.Id).ToListAsync(ct);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var settled = batches.Where(x => x.Status == "COMPLETED" && x.SettledAt >= monthStart).ToList();
        var demands = await Db.FactoryDemands.CountAsync(x => x.FactoryId == factory.Id && x.IsActive, ct);
        var partners = await Db.FactoryDepotPartnerships.CountAsync(x => x.FactoryId == factory.Id, ct);
        return ServiceResult<DashboardResponse>.Success(data: new DashboardResponse
        {
            OrderCount = batches.Count,
            ActiveDemandCount = demands,
            PartnerCount = partners,
            PendingQcCount = batches.Count(x => x.Status is "RECEIVED" or "WEIGHED" or "DELIVERED"),
            MonthlyPurchasedKg = settled.Sum(x => x.ActualWeightKg ?? 0),
            MonthlyNetPayment = settled.Sum(x => x.NetAmount ?? 0),
            RecentOrders = batches.OrderByDescending(x => x.CreatedAt).Take(5).Select(FactoryOrderMapper.Map).ToList()
        });
    }
}
