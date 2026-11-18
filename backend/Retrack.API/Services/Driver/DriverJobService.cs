using Retrack.API.Repositories.Interfaces;
using Retrack.API.Models;
using Retrack.API.DTOs;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Driver;

public sealed record JobPlace(string Name, string Address, decimal? Latitude, decimal? Longitude);
public sealed record DriverJobDto(Guid Id, Guid BatchId, string? BatchCode, string MaterialType,
    decimal WeightKg, string Status, bool IsMine, JobPlace Depot, JobPlace Factory, DateTime CreatedAt);

public sealed class DriverJobService(IDriverJobRepository repository, IDriverDeliveryRepository delivery, IStaffUnitOfWork work)
{
    public async Task RequireAsync(Guid userId, CancellationToken ct)
    {
        if (!await repository.HasActiveStaffAsync(userId, ct)) throw new DepotForbiddenException();
    }
    public async Task<PagedResult<DriverJobDto>> ListAsync(Guid userId, bool mine, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ArgumentException("Trang không hợp lệ.");
        await RequireAsync(userId, ct);
        return await repository.ListAsync(userId, mine, page, ct);
    }
    public async Task<DriverJobDto> DetailAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireAsync(userId, ct);
        return await repository.DetailAsync(userId, id, ct)
            ?? throw new KeyNotFoundException("Chuyến không còn khả dụng hoặc không thuộc phạm vi của bạn.");
    }
    public async Task<DriverJobDto> AcceptAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireAsync(userId, ct);
        var meta = await repository.ScopeAsync(userId, id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy chuyến trong kho của bạn.");
        await using var tx = await work.BeginAsync(ct);
        await repository.LockDepotAsync(meta.DepotId, ct);
        var changed = await repository.ClaimAsync(userId, id, DateTime.UtcNow, ct);
        if (changed == 0)
        {
            if (!await repository.IsAcceptedRetryAsync(userId, id, ct))
                throw new DepotConflictException("Chuyến đã được nhận hoặc lô không còn sẵn sàng vận chuyển.");
        }
        else
        {
            delivery.AddNotification(new Notification { UserId = meta.OwnerId, TransportJobId = id,
                Title = "Tài xế đã nhận chuyến", Message = "Một tài xế của kho đã nhận chuyến vận chuyển. Chưa xác nhận lấy hàng." });
            await work.SaveAsync(ct);
        }
        var result = await DetailAsync(userId, id, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
