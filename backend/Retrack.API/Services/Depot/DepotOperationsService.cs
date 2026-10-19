using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Services.Depot;
public sealed class DepotOperationsService(IDepotOperationsRepository repository, IDepotService scope, IDepotBatchRepository batches, IDepotUnitOfWork work)
{
    public async Task<PagedResult<DepotNoticeDto>> NoticesAsync(Guid owner, Guid depot, DepotQuery query)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (query.Status is not (null or "" or "READ" or "UNREAD")) throw new ArgumentException("Trạng thái đọc thông báo không hợp lệ.");
        return await repository.NoticesAsync(owner, depot, query);
    }
    public async Task<int> UnreadAsync(Guid owner, Guid depot) { await scope.RequireOwnerAsync(owner, depot); return await repository.UnreadAsync(owner, depot); }
    public async Task ReadAsync(Guid owner, Guid depot, Guid id)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (!await repository.ReadAsync(owner, depot, id)) throw new KeyNotFoundException("Không tìm thấy thông báo trong phạm vi kho.");
    }
    public async Task<PagedResult<DepotDriverEventDto>> EventsAsync(Guid owner, Guid depot, Guid batchId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(owner, depot); return await repository.EventsAsync(depot, batchId, query);
    }
    private async Task RequireEvent(Guid owner, Guid depot, Guid id)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (await repository.FindEventAsync(depot, id) == null) throw new KeyNotFoundException("Không tìm thấy sự kiện vận chuyển trong kho.");
    }
    public async Task<PagedResult<IncidentFollowupDto>> FollowupsAsync(Guid owner, Guid depot, Guid id, DepotQuery query)
    {
        await RequireEvent(owner, depot, id); return await repository.FollowupsAsync(id, query);
    }
    public async Task FollowupAsync(Guid owner, Guid depot, Guid id, CreateIncidentFollowupDto dto)
    {
        await RequireEvent(owner, depot, id);
        if (dto.OperationId == Guid.Empty || dto.Status is not ("NOTED" or "CONTACTING" or "CLOSED") || string.IsNullOrWhiteSpace(dto.Note) || dto.Note.Length > 1000)
            throw new ArgumentException("Cần trạng thái theo dõi và nội dung xử lý tối đa 1000 ký tự.");
        await using var tx = await work.BeginAsync(); await batches.LockDepotAsync(depot);
        var evt = await repository.FindEventAsync(depot, id) ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        if (evt.Action is not ("incident" or "cancel" or "reject")) throw new DepotConflictException("Chỉ theo dõi xử lý sự cố, trả chuyến hoặc từ chối chuyến.");
        var old = await repository.FindFollowupAsync(dto.OperationId);
        if (old != null)
        {
            if (old.EventId != id || old.CreatedBy != owner || old.Status != dto.Status || old.Note != dto.Note.Trim()) throw new DepotConflictException("Mã thao tác đã dùng cho ghi chú khác.");
            await tx.CommitAsync(); return;
        }
        repository.AddFollowup(new() { Id = dto.OperationId, EventId = id, Status = dto.Status, Note = dto.Note.Trim(), CreatedBy = owner });
        await work.SaveAsync(); await tx.CommitAsync();
    }
}
