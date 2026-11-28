using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryNotificationRepository
{
    Task<bool> IsActiveFactoryAsync(Guid userId, CancellationToken ct);
    Task<PageResponse<FactoryNotificationResponse>> ListAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct);
    Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<int> ReadAllAsync(Guid userId, CancellationToken ct);
}
