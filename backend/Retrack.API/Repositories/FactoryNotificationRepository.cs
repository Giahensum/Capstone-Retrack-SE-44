using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryNotificationRepository(AppDbContext db) : IFactoryNotificationRepository
{
    public Task<bool> IsActiveFactoryAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive && x.Role == "FACTORY", ct);
    public async Task<PageResponse<FactoryNotificationResponse>> ListAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct)
    {
        var source = db.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        if (unreadOnly) source = source.Where(x => !x.IsRead);
        var total = await source.CountAsync(ct);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, total);
        var items = await source.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(skip).Take(pageSize)
            .Select(x => new FactoryNotificationResponse(x.Id, x.Title, x.Message ?? "", x.Category,
                x.BatchId ?? db.TransportJobs.Where(job => job.Id == x.TransportJobId).Select(job => (Guid?)job.BatchId).FirstOrDefault(),
                x.DepotId, x.TargetType, x.IsRead, x.CreatedAt)).ToListAsync(ct);
        return new PageResponse<FactoryNotificationResponse> { Items = items, TotalCount = total, Page = page,
            PageSize = pageSize, TotalPages = (int)Math.Ceiling((double)total / pageSize) };
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && !x.IsRead, ct);
    public Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public Task<int> ReadAllAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), ct);
}
