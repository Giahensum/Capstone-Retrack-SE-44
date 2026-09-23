using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false);
}
