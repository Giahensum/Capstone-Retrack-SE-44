using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class BatchService(IDepotBatchRepository batches, IDepotUnitOfWork work, IDepotService scope, IInventoryService inventory) : IBatchService
{
    private static readonly string[] States = ["DRAFT", "LISTED", "MARKETPLACE", "PENDING_APPROVAL", "PENDING_FACTORY", "TRANSPORT_READY", "ACCEPTED", "READY_FOR_PICKUP", "IN_PROGRESS", "IN_TRANSIT", "DELIVERED", "RECEIVED", "WEIGHED", "VERIFIED", "COMPLETED", "PAID", "REJECTED", "CANCELLED"];

    public async Task<PagedResult<DepotBatchDto>> ListAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!string.IsNullOrEmpty(query.Status) && !States.Contains(query.Status)) throw new ArgumentException("Trạng thái lô không hợp lệ.");
        return await batches.ListAsync(depotId, query);
    }

    public async Task<DepotBatchDto> CreateAsync(Guid ownerId, Guid depotId, CreateDepotBatchDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (dto.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(dto.MaterialType) || dto.MaterialType.Length > 100 || dto.WeightKg <= 0)
            throw new ArgumentException("Cần mã thao tác, loại vật liệu và khối lượng dương.");
        await using var tx = await work.BeginAsync();
        // Mọi thao tác giữ/hủy tồn kho của kho này dùng chung khóa dòng trong database.
        await batches.LockDepotAsync(depotId);
        var existing = await batches.FindOperationAsync(dto.OperationId);
        if (existing != null)
        {
            if (existing.DepotId != depotId || existing.MaterialType != dto.MaterialType ||
                existing.DeclaredWeightKg != dto.WeightKg || existing.TargetFactoryId != dto.TargetFactoryId || existing.Description != dto.Description)
                throw new DepotConflictException("Mã thao tác đã được dùng cho một lô khác.");
            return Map(existing);
        }
        var stock = (await inventory.GetAsync(ownerId, depotId)).SingleOrDefault(i => i.MaterialType == dto.MaterialType);
        if (stock == null || stock.AvailableKg < dto.WeightKg) throw new DepotConflictException("Tồn kho khả dụng không đủ.");
        var status = "LISTED";
        Retrack.API.Models.Factory? factory = null;
        if (dto.TargetFactoryId.HasValue)
        {
            factory = await batches.FindActiveFactoryAsync(dto.TargetFactoryId.Value)
                ?? throw new ArgumentException("Nhà máy không tồn tại hoặc đã ngừng hoạt động.");
            var partner = await batches.FindPartnerAsync(depotId, dto.TargetFactoryId.Value);
            if (partner?.Status == "BLOCKED") throw new DepotConflictException("Nhà máy đã chặn hợp tác với kho.");
            status = partner?.Status == "APPROVED" ? "TRANSPORT_READY" : "PENDING_APPROVAL";
            if (partner == null) batches.AddPartner(new FactoryDepotPartnership { DepotId = depotId, FactoryId = factory.Id });
        }
        // Bộ đếm database tránh trùng giữa các kho/tiến trình; hoàn tác có thể để lại số bị khuyết.
        var sequence = await batches.NextNumberAsync();
        var code = $"LO-{DateTime.UtcNow.AddHours(7):yyMM}-{sequence:D3}";
        var batch = new InventoryBatch { Id = dto.OperationId, Code = code, DepotId = depotId, MaterialType = dto.MaterialType,
            DeclaredWeightKg = dto.WeightKg, Description = dto.Description, TargetFactoryId = dto.TargetFactoryId, Status = status };
        batches.AddBatch(batch);
        if (status == "TRANSPORT_READY") batches.AddTransport(new TransportJob { BatchId = batch.Id });
        await work.SaveAsync();
        await tx.CommitAsync();
        batch.TargetFactory = factory;
        return Map(batch);
    }

    public async Task CancelAsync(Guid ownerId, Guid depotId, Guid batchId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        await using var tx = await work.BeginAsync();
        await batches.LockDepotAsync(depotId);
        var batch = await batches.LockBatchAsync(batchId) ?? throw new KeyNotFoundException("Không tìm thấy lô.");
        if (batch.DepotId != depotId) throw new DepotForbiddenException();
        if (batch.Status == "CANCELLED") return;
        if (batch.Status is not ("DRAFT" or "LISTED" or "MARKETPLACE") || batch.TargetFactoryId != null ||
            await batches.HasTransportAsync(batchId))
            throw new DepotConflictException("Chỉ hủy lô chưa có nhà máy nhận.");
        batch.Status = "CANCELLED";
        batch.UpdatedAt = DateTime.UtcNow;
        await work.SaveAsync();
        await tx.CommitAsync();
    }

    private static DepotBatchDto Map(InventoryBatch b) => new(b.Id, b.DepotId, b.MaterialType,
        b.DeclaredWeightKg, b.Description, b.Status, b.TargetFactoryId, b.TargetFactory?.Name, b.CreatedAt, b.Code);
}
