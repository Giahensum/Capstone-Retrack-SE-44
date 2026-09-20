using Retrack.API.Services.Interfaces;
namespace Retrack.API.Services.Shared;

public class NotificationService : INotificationService
{
    // TODO: Implement notification logic
    public async Task SendAsync(Guid userId, string title, string message) => throw new NotImplementedException();
    public async Task<IEnumerable<object>> GetByUserIdAsync(Guid userId) => throw new NotImplementedException();
    public async Task MarkAsReadAsync(Guid notificationId) => throw new NotImplementedException();
}
