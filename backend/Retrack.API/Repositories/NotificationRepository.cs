using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

// Dùng cùng DbContext scoped để giữ nguyên ranh giới Save của các caller hiện hành.
public sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false)
    {
        var source = db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) source = source.Where(n => !n.IsRead);
        return await source.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }
    public async Task<Notification?> FindAsync(Guid id) => await db.Notifications.FindAsync(id);
    public void Add(Notification notification) => db.Notifications.Add(notification);
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
