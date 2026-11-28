using Retrack.API.DTOs;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Seller;

public sealed class SellerNotificationService(ISellerNotificationRepository repository)
{
    public Task<PagedResult<SellerNotificationDto>> ListAsync(Guid userId, int page, int pageSize, bool? unreadOnly, string? category, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        return repository.ListAsync(userId, page, pageSize, unreadOnly, category, ct);
    }
    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct) => repository.UnreadCountAsync(userId, ct);
    public async Task<bool> SetReadAsync(Guid userId, Guid id, bool isRead, CancellationToken ct) =>
        await repository.SetReadAsync(userId, id, isRead, ct) != 0;
    public Task<int> ReadAllAsync(Guid userId, CancellationToken ct) => repository.ReadAllAsync(userId, ct);
}
