using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Depot;

public sealed class DepotBuyingPriceService(IDepotBuyingPriceRepository prices, IDepotBatchRepository batches,
    IDepotUnitOfWork work, IDepotService scope) : IDepotBuyingPriceService
{
    public async Task<PagedResult<DepotBuyingPriceDto>> ListAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.Search?.Length > 100) throw new ArgumentException("Phân trang không hợp lệ.");
        return await prices.ListAsync(depotId, query);
    }
    public async Task<DepotBuyingPriceDto> SaveAsync(Guid ownerId, Guid depotId, string material, SaveDepotBuyingPriceDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var code = MaterialCatalog.RequireCode(material);
        if (dto.PricePerKg <= 0 || dto.PricePerKg > 1000000000 || decimal.Truncate(dto.PricePerKg) != dto.PricePerKg ||
            dto.ConditionNote == null || dto.ConditionNote.Length > 500 || dto.ExpectedUpdatedAt is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("Giá phải là số nguyên từ 1 đến 1 tỷ đồng/kg; điều kiện tối đa 500 ký tự.");
        await using var tx = await work.BeginAsync(); await batches.LockDepotAsync(depotId);
        var price = await prices.FindAsync(depotId, code);
        var note = dto.ConditionNote.Trim();
        if (price != null && price.PricePerKg == dto.PricePerKg && price.ConditionNote == note && price.IsActive == dto.IsActive)
            return Map(price);
        if (price?.UpdatedAt != dto.ExpectedUpdatedAt)
            throw new DepotConflictException("Bảng giá đã thay đổi ở cửa sổ khác. Đóng và mở lại để lấy giá mới nhất.");
        var now = DateTime.UtcNow; now = new DateTime(now.Ticks / 10 * 10, DateTimeKind.Utc);
        if (price == null) { price = new DepotBuyingPrice { DepotId = depotId, MaterialType = code }; prices.Add(price); }
        price.UpdatedAt = now > price.UpdatedAt ? now : price.UpdatedAt.AddTicks(10);
        price.PricePerKg = dto.PricePerKg; price.ConditionNote = note; price.IsActive = dto.IsActive;
        await work.SaveAsync(); await tx.CommitAsync(); return Map(price);
    }
    private static DepotBuyingPriceDto Map(DepotBuyingPrice price) => new(price.MaterialType, price.PricePerKg, price.ConditionNote, price.IsActive, price.UpdatedAt);
}
