namespace Retrack.API.Services.Interfaces;

public interface INotificationService
{
    Task SendAsync(Guid userId, string title, string message);
    Task<IEnumerable<object>> GetByUserIdAsync(Guid userId);
    Task MarkAsReadAsync(Guid notificationId);
}


