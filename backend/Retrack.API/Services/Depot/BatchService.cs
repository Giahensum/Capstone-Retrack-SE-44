using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class BatchService(IDepotBatchRepository batches, IDepotUnitOfWork work, IDepotService scope, IInventoryService inventory, ICloudinaryService images) : IBatchService
{
    public async Task<DepotBatchDetailDto> DetailAsync(Guid ownerId, Guid depotId, Guid batchId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        return await batches.DetailAsync(depotId, batchId) ?? throw new KeyNotFoundException("Không tìm thấy lô trong kho đang quản lý.");
    }
    private static readonly string[] States = ["DRAFT", "LISTED", "MARKETPLACE", "PENDING_APPROVAL", "PENDING_FACTORY", "TRANSPORT_READY", "ACCEPTED", "READY_FOR_PICKUP", "IN_PROGRESS", "IN_TRANSIT", "DELIVERED", "RECEIVED", "WEIGHED", "VERIFIED", "COMPLETED", "PAID", "REJECTED", "CANCELLED"];

    public async Task<PagedResult<DepotBatchDto>> ListAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!string.IsNullOrEmpty(query.Status) && !States.Contains(query.Status)) throw new ArgumentException("Trạng thái lô không hợp lệ.");
        return await batches.ListAsync(depotId, query);
    }

    public async Task<DepotBatchDto> CreateAsync(Guid ownerId, Guid depotId, CreateDepotBatchDto dto, IReadOnlyList<IFormFile>? photos = null)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (photos?.Count > 5) throw new ArgumentException("Mỗi lô chỉ được tải lên tối đa 5 ảnh vật liệu.");
        if (dto.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(dto.MaterialType) || dto.MaterialType.Length > 100 || dto.WeightKg <= 0)
            throw new ArgumentException("Cần mã thao tác, loại vật liệu và khối lượng dương.");
        dto.MaterialType = Retrack.API.Services.Shared.MaterialCatalog.RequireCode(dto.MaterialType);
        await using var tx = await work.BeginAsync();
        // Mọi thao tác giữ/hủy tồn kho của kho này dùng chung khóa dòng trong database.
        await batches.LockDepotAsync(depotId);
        var existing = await batches.FindOperationAsync(dto.OperationId);
        if (existing != null)
        {
            if (existing.DepotId != depotId || Retrack.API.Services.Shared.MaterialCatalog.Normalize(existing.MaterialType) != dto.MaterialType ||
                existing.DeclaredWeightKg != dto.WeightKg || (existing.TargetFactoryId ?? existing.DirectOfferFactoryId) != dto.TargetFactoryId || existing.Description != dto.Description)
                throw new DepotConflictException("Mã thao tác đã được dùng cho một lô khác.");
            return Map(existing);
        }
        var stock = (await inventory.GetAsync(ownerId, depotId)).SingleOrDefault(i => i.MaterialType == dto.MaterialType);
        if (stock == null || stock.AvailableKg < dto.WeightKg) throw new DepotConflictException("Tồn kho khả dụng không đủ.");
        var status = "LISTED";
        Retrack.API.Models.Factory? factory = null;
        var isApproved = false;
        if (dto.TargetFactoryId.HasValue)
        {
            factory = await batches.FindActiveFactoryAsync(dto.TargetFactoryId.Value)
                ?? throw new ArgumentException("Nhà máy không tồn tại hoặc đã ngừng hoạt động.");
            var acceptedMaterials = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Retrack.API.Services.Shared.MaterialCatalog.Normalize).ToArray();
            if (acceptedMaterials.Length > 0 && !acceptedMaterials.Contains(dto.MaterialType))
                throw new DepotConflictException("Nhà máy không tiếp nhận loại vật liệu này.");
            if (factory.CapacityKgPerMonth > 0 && dto.WeightKg > factory.CapacityKgPerMonth)
                throw new DepotConflictException("Khối lượng lô vượt mức nhà máy hiện có thể tiếp nhận.");
            var partner = await batches.FindPartnerAsync(depotId, dto.TargetFactoryId.Value);
            if (partner?.Status == "BLOCKED" || partner?.BlockedByDepot == true || partner?.BlockedByFactory == true) throw new DepotConflictException("Quan hệ đang bị chặn, không thể tạo lô mới cho nhà máy.");
            isApproved = partner?.Status == "APPROVED";
            status = isApproved ? "TRANSPORT_READY" : "PENDING_APPROVAL";
            if (partner == null) batches.AddPartner(new FactoryDepotPartnership { DepotId = depotId, FactoryId = factory.Id, Status = "PENDING" });
        }
        var imageUrls = new List<string>();
        if (photos != null)
        {
            var validated = new List<(byte[] Bytes, string Extension)>();
            foreach (var photo in photos)
            {
                const int maxBytes = 10 * 1024 * 1024;
                if (photo.Length is <= 0 or > maxBytes) throw new ArgumentException("Mỗi ảnh vật liệu phải có dung lượng từ 1 byte đến 10 MB.");
                await using var data = new MemoryStream();
                await using var input = photo.OpenReadStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(chunk)) > 0)
                {
                    if (data.Length + read > maxBytes) throw new ArgumentException("Ảnh vật liệu vượt quá 10 MB.");
                    await data.WriteAsync(chunk.AsMemory(0, read));
                }
                var bytes = data.ToArray();
                var extension = photo.ContentType switch
                {
                    "image/png" when bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => ".png",
                    "image/jpeg" when bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) => ".jpg",
                    "image/webp" when bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) => ".webp",
                    _ => throw new ArgumentException("Chỉ chấp nhận ảnh vật liệu PNG, JPEG hoặc WebP đúng định dạng.")
                };
                validated.Add((bytes, extension));
            }
            foreach (var (bytes, extension) in validated)
            {
                using var data = new MemoryStream(bytes, writable: false);
                imageUrls.Add(await images.UploadImageAsync(data, $"depot-batch-{dto.OperationId:N}-{imageUrls.Count + 1}{extension}"));
            }
        }
        // Bộ đếm database tránh trùng giữa các kho/tiến trình; hoàn tác có thể để lại số bị khuyết.
        var sequence = await batches.NextNumberAsync();
        var code = $"LO-{DateTime.UtcNow.AddHours(7):yyMM}-{sequence:D3}";
        var batch = new InventoryBatch {
            Id = dto.OperationId, Code = code, DepotId = depotId, MaterialType = dto.MaterialType,
            DeclaredWeightKg = dto.WeightKg, Description = dto.Description,
            TargetFactoryId = isApproved ? dto.TargetFactoryId : null,
            DirectOfferFactoryId = !isApproved && dto.TargetFactoryId.HasValue ? dto.TargetFactoryId : null,
            Status = status, ImageUrls = imageUrls.ToArray()
        };
        batches.AddBatch(batch);
        if (status == "TRANSPORT_READY") await batches.AddTransportAsync(new TransportJob { BatchId = batch.Id }, depotId);
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
        var publicOffer = batch.Status is "DRAFT" or "LISTED" or "MARKETPLACE" && batch.TargetFactoryId == null;
        var pendingOffer = batch.Status is "PENDING_APPROVAL" or "PENDING_FACTORY";
        if ((!publicOffer && !pendingOffer) || batch.FactoryReceivedAt != null || batch.QualityCheck != null ||
            await batches.HasTransportAsync(batchId))
            throw new DepotConflictException("Chỉ hủy lô chưa có nhà máy nhận.");
        batch.Status = "CANCELLED";
        batch.UpdatedAt = DateTime.UtcNow;
        await work.SaveAsync();
        await tx.CommitAsync();
    }

    private static DepotBatchDto Map(InventoryBatch b) => new(b.Id, b.DepotId, b.MaterialType,
        b.DeclaredWeightKg, b.Description, b.Status, b.TargetFactoryId ?? b.DirectOfferFactoryId,
        b.TargetFactory?.Name ?? b.DirectOfferFactory?.Name, b.CreatedAt, b.Code, b.ImageUrls);
}
