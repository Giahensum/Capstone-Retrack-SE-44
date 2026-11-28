using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs.Factory;

namespace Retrack.API.Services.Factory;

public sealed class FactoryNotificationService(IFactoryNotificationRepository repository)
{
    private async Task CheckAccessAsync(Guid userId, CancellationToken ct)
    {
        var allowed = await repository.IsActiveFactoryAsync(userId, ct);
        if (!allowed) throw new UnauthorizedAccessException("Tài khoản nhà máy không hợp lệ.");
    }

    public async Task<PageResponse<FactoryNotificationResponse>> ListAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Phân trang thông báo không hợp lệ.");
        return await repository.ListAsync(userId, page, pageSize, unreadOnly, ct);
    }

    public async Task<int> UnreadCountAsync(Guid userId, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        return await repository.UnreadCountAsync(userId, ct);
    }

    public async Task MarkReadAsync(Guid userId, Guid id, bool isRead, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        var notice = await repository.FindAsync(userId, id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy thông báo.");
        if (notice.IsRead == isRead) return;
        notice.IsRead = isRead;
        await repository.SaveAsync(ct);
    }

    public async Task<int> ReadAllAsync(Guid userId, CancellationToken ct)
    {
        await CheckAccessAsync(userId, ct);
        return await repository.ReadAllAsync(userId, ct);
    }
}
