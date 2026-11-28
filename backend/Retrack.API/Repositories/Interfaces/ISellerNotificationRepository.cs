using Retrack.API.DTOs;

namespace Retrack.API.Repositories.Interfaces;

public interface ISellerNotificationRepository
{
    Task<PagedResult<SellerNotificationDto>> ListAsync(Guid userId, int page, int pageSize, bool? unreadOnly, string? category, CancellationToken ct);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct);
    Task<int> SetReadAsync(Guid userId, Guid id, bool isRead, CancellationToken ct);
    Task<int> ReadAllAsync(Guid userId, CancellationToken ct);
}
