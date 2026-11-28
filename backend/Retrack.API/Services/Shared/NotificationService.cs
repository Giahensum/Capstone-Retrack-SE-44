using Retrack.API.Repositories.Interfaces;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Shared;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;

    public NotificationService(INotificationRepository repository) => _repository = repository;

    public async Task SendAsync(Guid userId, string title, string message)
    {
        _repository.Add(new Notification { UserId = userId, Title = title, Message = message });
        await _repository.SaveAsync();
    }
    public async Task SendInvoiceAsync(Guid userId, string title, string message)
    {
        _repository.Add(new Notification { UserId = userId, Title = title, Message = message, Category = "PAYMENT", TargetType = "FEES" });
        await _repository.SaveAsync();
    }

    public async Task SendPickupAsync(Guid userId, Guid pickupId, string title, string message)
    {
        _repository.Add(Notification.ForPickup(userId, pickupId, title, message));
        await _repository.SaveAsync();
    }

    public async Task<IEnumerable<object>> GetByUserIdAsync(Guid userId)
        => await _repository.GetByUserIdAsync(userId);

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _repository.FindAsync(notificationId);
        if (notification == null) return;
        notification.IsRead = true;
        await _repository.SaveAsync();
    }
}
