using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class SellerNotificationRepository(AppDbContext db) : ISellerNotificationRepository
{
    public async Task<PagedResult<SellerNotificationDto>> ListAsync(Guid userId, int page, int pageSize, bool? unreadOnly, string? category, CancellationToken ct)
    {
        var source = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly == true) source = source.Where(n => !n.IsRead);
        if (!string.IsNullOrWhiteSpace(category)) source = source.Where(n => n.Category == category);
        var total = await source.CountAsync(ct);
        // Dùng long để trang lớn không làm tràn phép nhân trước khi giới hạn offset.
        var offset = (int)Math.Min((long)(page - 1) * pageSize, total);
        var items = await source.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id)
            .Skip(offset).Take(pageSize)
            .Select(n => new SellerNotificationDto
            {
                Id = n.Id, Title = n.Title, Message = n.Message ?? string.Empty, Category = n.Category,
                PickupRequestId = n.PickupRequestId, TargetType = n.TargetType, TargetId = n.TargetId,
                IsRead = n.IsRead, CreatedAt = n.CreatedAt
            }).ToListAsync(ct);
        return new PagedResult<SellerNotificationDto> { Page=page, PageSize=pageSize, TotalCount=total, Items=items };
    }
    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    public Task<int> SetReadAsync(Guid userId, Guid id, bool isRead, CancellationToken ct) =>
        db.Notifications.Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, isRead), ct);
    public Task<int> ReadAllAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
}
