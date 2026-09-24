using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Shared;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public async Task SendAsync(Guid userId, string title, string message)
    {
        _db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = message });
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<object>> GetByUserIdAsync(Guid userId)
        => await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _db.Notifications.FindAsync(notificationId);
        if (notification == null) return;
        notification.IsRead = true;
        await _db.SaveChangesAsync();
    }
}
