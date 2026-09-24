using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/dashboard")]
[Authorize(Roles = "FACTORY")]
public class FactoryDashboardController(Retrack.API.Data.AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var batches = await OrderQuery(factory.Id).ToListAsync(ct);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var settled = batches.Where(x => x.Status == "COMPLETED" && x.SettledAt >= monthStart).ToList();
        var demands = await Db.FactoryDemands.CountAsync(x => x.FactoryId == factory.Id && x.IsActive, ct);
        var partners = await Db.FactoryDepotPartnerships.CountAsync(x => x.FactoryId == factory.Id, ct);
        return Ok(new { success = true, data = new
        {
            orderCount = batches.Count,
            activeDemandCount = demands,
            partnerCount = partners,
            pendingQcCount = batches.Count(x => x.Status is "RECEIVED" or "WEIGHED" or "DELIVERED"),
            monthlyPurchasedKg = settled.Sum(x => x.ActualWeightKg ?? 0),
            monthlyNetPayment = settled.Sum(x => x.NetAmount ?? 0),
            recentOrders = batches.OrderByDescending(x => x.CreatedAt).Take(5).Select(OrderView)
        }});
    }
}
