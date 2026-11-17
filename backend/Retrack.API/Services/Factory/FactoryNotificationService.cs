using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;

namespace Retrack.API.Services.Factory;

public sealed class FactoryNotificationService(AppDbContext db)
{
    private async Task CheckAccessAsync(Guid userId, CancellationToken ct)
    {
        var allowed = await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive && x.Role == "FACTORY", ct);
        if (!allowed) throw new UnauthorizedAccessException("Tài khoản nhà máy không hợp lệ.");
    }

    public async Task<PageResponse<FactoryNotificationResponse>> ListAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Phân trang thông báo không hợp lệ.");
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

    public async Task<int> UnreadCountAsync(Guid userId, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        return await db.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && !x.IsRead, ct);
    }

    public async Task MarkReadAsync(Guid userId, Guid id, bool isRead, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        var notice = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy thông báo.");
        if (notice.IsRead == isRead) return;
        notice.IsRead = isRead;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> ReadAllAsync(Guid userId, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        return await db.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), ct);
    }
}
