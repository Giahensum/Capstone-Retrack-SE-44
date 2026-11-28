using Retrack.API.DTOs.Driver;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;
public sealed class DriverReportingRepository(AppDbContext db, DriverJobRepository jobs) : IDriverReportingRepository
{
    // Thời điểm checkout do server ghi; dữ liệu cũ chưa có sự kiện dùng updated_at.
    private IQueryable<DriverHistoryRow> Completed(Guid userId) => jobs.Scoped(userId).AsNoTracking()
        .Where(j => j.DriverId == userId && j.Status == "DELIVERED")
        .Select(j => new DriverHistoryRow { Id = j.Id, BatchCode = j.Batch.Code, MaterialType = j.Batch.MaterialType, WeightKg = j.Batch.DeclaredWeightKg,
            DepotName = j.Batch.Depot.Name, FactoryName = j.Batch.TargetFactory == null ? "Nhà máy không còn khả dụng" : j.Batch.TargetFactory.Name,
            DeliveredAt = db.DriverDeliveryEvents.Where(e => e.JobId == j.Id && e.DriverId == userId && e.Action == "checkout")
                .Select(e => (DateTime?)e.CreatedAt).Max() ?? j.UpdatedAt,
            FactoryReceivedAt = j.Batch.FactoryReceivedAt,
            LegacyDate = !db.DriverDeliveryEvents.Any(e => e.JobId == j.Id && e.DriverId == userId && e.Action == "checkout") });

    public async Task<DriverDashboardDto> DashboardAsync(Guid userId, DateTime today, CancellationToken ct)
    {
        var active = jobs.Scoped(userId).AsNoTracking().Where(j => j.DriverId == userId
            && (j.Status == "ACCEPTED" || j.Status == "PICKED_UP" || j.Status == "IN_TRANSIT" || j.Status == "ON_THE_WAY"));
        var completed = Completed(userId);
        return new(await jobs.Pool(userId).CountAsync(ct), await active.CountAsync(ct),
            await completed.CountAsync(j => j.DeliveredAt >= today && j.DeliveredAt < today.AddDays(1), ct),
            await completed.CountAsync(j => j.FactoryReceivedAt == null, ct),
            await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct),
            await active.OrderBy(j => j.Status == "ACCEPTED" ? 1 : 0).ThenBy(j => j.CreatedAt).ThenBy(j => j.Id)
                .Take(3).Select(j => new DriverActiveJob(j.Id, j.Batch.Code, j.Status, j.Batch.Depot.Name,
                    j.Batch.TargetFactory == null ? "Nhà máy không còn khả dụng" : j.Batch.TargetFactory.Name)).ToListAsync(ct));
    }

    public async Task<PagedResult<DriverHistoryRow>> HistoryAsync(Guid userId, int page, int pageSize,
        string period, string receipt, DateTime now, CancellationToken ct)
    {
        var query = Completed(userId);
        if (period == "month") query = Month(query, now);
        if (receipt == "waiting") query = query.Where(j => j.FactoryReceivedAt == null);
        if (receipt == "received") query = query.Where(j => j.FactoryReceivedAt != null);
        return new() { Page = page, PageSize = pageSize, TotalCount = await query.CountAsync(ct),
            Items = await query.OrderByDescending(j => j.DeliveredAt).ThenBy(j => j.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct) };
    }

    private static IQueryable<DriverHistoryRow> Month(IQueryable<DriverHistoryRow> source, DateTime now)
    {
        var start = Retrack.API.Helpers.VietnamTime.MonthStart(now);
        var end = start.AddHours(7).AddMonths(1).AddHours(-7);
        return source.Where(j => j.DeliveredAt >= start && j.DeliveredAt < end);
    }

    public async Task<DriverStatsDto> StatsAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var query = Completed(userId);
        async Task<DriverTotals> Total(IQueryable<DriverHistoryRow> source) => new(await source.CountAsync(ct),
            await source.SumAsync(j => (double?)j.WeightKg, ct) ?? 0,
            await source.CountAsync(j => j.FactoryReceivedAt != null, ct));
        return new(await Total(query), await Total(Month(query, now)), "Asia/Ho_Chi_Minh");
    }
}
