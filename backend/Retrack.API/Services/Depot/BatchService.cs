using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class BatchService(AppDbContext db, IDepotService scope, IInventoryService inventory) : IBatchService
{
    private static readonly string[] States = ["DRAFT", "LISTED", "MARKETPLACE", "PENDING_APPROVAL", "TRANSPORT_READY", "ACCEPTED", "READY_FOR_PICKUP", "IN_PROGRESS", "IN_TRANSIT", "DELIVERED", "VERIFIED", "COMPLETED", "REJECTED", "CANCELLED"];

    public async Task<PagedResult<DepotBatchDto>> ListAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new ArgumentException("Phân trang không hợp lệ.");
        var source = db.InventoryBatches.AsNoTracking().Where(b => b.DepotId == depotId);
        if (!string.IsNullOrEmpty(query.Status))
        {
            if (!States.Contains(query.Status)) throw new ArgumentException("Trạng thái lô không hợp lệ.");
            source = source.Where(b => b.Status == query.Status);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
            source = source.Where(b => b.MaterialType.Contains(query.Search) || b.Id.ToString().Contains(query.Search) || (b.Code != null && b.Code.Contains(query.Search)));
        var total = await source.CountAsync();
        source = query.Sort switch
        {
            "newest" => source.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id),
            "oldest" => source.OrderBy(b => b.CreatedAt).ThenBy(b => b.Id),
            _ => throw new ArgumentException("Lô hàng chỉ hỗ trợ sắp xếp theo thời gian.")
        };
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = total,
            Items = offset > int.MaxValue ? [] : await source.Skip((int)offset).Take(query.PageSize)
                .Select(b => new DepotBatchDto(b.Id, b.DepotId, b.MaterialType, b.DeclaredWeightKg,
                    b.Description, b.Status, b.TargetFactoryId, b.TargetFactory == null ? null : b.TargetFactory.Name, b.CreatedAt, b.Code)).ToListAsync() };
    }

    public async Task<DepotBatchDto> CreateAsync(Guid ownerId, Guid depotId, CreateDepotBatchDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (dto.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(dto.MaterialType) || dto.MaterialType.Length > 100 || dto.WeightKg <= 0)
            throw new ArgumentException("Cần mã thao tác, loại vật liệu và khối lượng dương.");
        await using var tx = await db.Database.BeginTransactionAsync();
        // Mọi thao tác giữ/hủy tồn kho của kho này dùng chung khóa dòng trong database.
        await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync();
        var existing = await db.InventoryBatches.AsNoTracking().Include(b => b.TargetFactory).SingleOrDefaultAsync(b => b.Id == dto.OperationId);
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
        Factory? factory = null;
        if (dto.TargetFactoryId.HasValue)
        {
            factory = await db.Factories.SingleOrDefaultAsync(f => f.Id == dto.TargetFactoryId && f.Owner.IsActive)
                ?? throw new ArgumentException("Nhà máy không tồn tại hoặc đã ngừng hoạt động.");
            var partner = await db.FactoryDepotPartnerships.SingleOrDefaultAsync(p => p.DepotId == depotId && p.FactoryId == dto.TargetFactoryId);
            if (partner?.Status == "BLOCKED") throw new DepotConflictException("Nhà máy đã chặn hợp tác với kho.");
            status = partner?.Status == "APPROVED" ? "TRANSPORT_READY" : "PENDING_APPROVAL";
            if (partner == null) db.FactoryDepotPartnerships.Add(new FactoryDepotPartnership { DepotId = depotId, FactoryId = factory.Id });
        }
        // Bộ đếm database tránh trùng giữa các kho/tiến trình; hoàn tác có thể để lại số bị khuyết.
        var sequence = await db.Database.SqlQueryRaw<long>("SELECT nextval('depot_batch_number') AS \"Value\"").SingleAsync();
        var code = $"LO-{DateTime.UtcNow.AddHours(7):yyMM}-{sequence:D3}";
        var batch = new InventoryBatch { Id = dto.OperationId, Code = code, DepotId = depotId, MaterialType = dto.MaterialType,
            DeclaredWeightKg = dto.WeightKg, Description = dto.Description, TargetFactoryId = dto.TargetFactoryId, Status = status };
        db.InventoryBatches.Add(batch);
        if (status == "TRANSPORT_READY") db.TransportJobs.Add(new TransportJob { BatchId = batch.Id });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        batch.TargetFactory = factory;
        return Map(batch);
    }

    public async Task CancelAsync(Guid ownerId, Guid depotId, Guid batchId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync();
        var batch = await db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {batchId} FOR UPDATE")
            .SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Không tìm thấy lô.");
        if (batch.DepotId != depotId) throw new DepotForbiddenException();
        if (batch.Status == "CANCELLED") return;
        if (batch.Status is not ("DRAFT" or "LISTED" or "MARKETPLACE") || batch.TargetFactoryId != null ||
            await db.TransportJobs.AnyAsync(t => t.BatchId == batchId))
            throw new DepotConflictException("Chỉ hủy lô chưa có nhà máy nhận.");
        batch.Status = "CANCELLED";
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static DepotBatchDto Map(InventoryBatch b) => new(b.Id, b.DepotId, b.MaterialType,
        b.DeclaredWeightKg, b.Description, b.Status, b.TargetFactoryId, b.TargetFactory?.Name, b.CreatedAt, b.Code);
}
