using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models.Enums;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/dashboard")]
[Authorize(Roles = "FACTORY")]
public class FactoryDashboardController(AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var orders = Db.BatchOrders.Where(x => x.FactoryId == factory.Id);
        var statusCounts = await orders.GroupBy(x => x.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).ToListAsync(ct);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var month = await Db.BatchOrders.Include(x => x.Batch).Include(x => x.WeightVerification)
            .Where(x => x.FactoryId == factory.Id && x.CreatedAt >= monthStart).ToListAsync(ct);
        var pendingQc = await orders.CountAsync(x => x.FactoryId == factory.Id && x.Status == BatchStatus.WEIGHED, ct);
        var demandCount = await Db.FactoryDemands.CountAsync(x => x.FactoryId == factory.Id && x.IsActive, ct);
        var recentOrders = await OrderQuery(factory.Id).OrderByDescending(x => x.CreatedAt).Take(5).ToListAsync(ct);
        var priceRows = await Db.MarketPrices.AsNoTracking().OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var prices = priceRows.GroupBy(x => x.MaterialType).Select(g => g.First())
            .OrderBy(x => x.MaterialType).ToList();
        return Ok(new { success = true, data = new
        {
            factory = new { factory.Id, factory.CompanyName },
            summary = new { totalOrders = await orders.CountAsync(ct), pendingQc, activeDemands = demandCount,
                monthSettledAmount = month.Where(x => x.Status == BatchStatus.PAID).Sum(x => x.TotalAmount ?? 0),
                monthReceivedWeightKg = month.Where(x => x.Status == BatchStatus.VERIFIED || x.Status == BatchStatus.PAID || x.Status == BatchStatus.REJECTED).Sum(x => x.WeightVerification?.FactoryWeightKg ?? x.Batch.EstimatedWeightKg) },
            statusCounts,
            recentOrders = recentOrders.Select(OrderView),
            marketPrices = prices.Select(x => new { materialType = x.MaterialType.ToString(), x.PricePerKg, x.EffectiveDate, x.Source })
        }});
    }
}
