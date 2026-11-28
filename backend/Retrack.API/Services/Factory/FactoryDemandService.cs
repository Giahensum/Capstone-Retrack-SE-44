using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Factory;

public class FactoryDemandService(AppDbContext db, IFactoryDemandRepository? reads = null) : FactoryServiceBase(db), IFactoryDemandService
{
    private readonly IFactoryDemandRepository demandReads = reads ?? new FactoryDemandRepository(db);
    public async Task<ServiceResult<PageResponse<DemandResponse>>> ListAsync(Guid userId, PageQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var materials = query.Material.HasValue ? MaterialCatalog.Values(query.Material.Value.ToString()) : null;
        var page = await demandReads.ListAsync(factory.Id, materials, query.Page, query.PageSize, ct);
        return ServiceResult<PageResponse<DemandResponse>>.Success(data: new PageResponse<DemandResponse>
        {
            Items = page.Items.Select(DemandView).ToList(),
            TotalCount = page.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)page.TotalCount / query.PageSize)
        });
    }

    public async Task<ServiceResult<DemandResponse>> CreateAsync(Guid userId, DemandRequest request, CancellationToken ct)
    {
        if (request.MinPricePerKg > request.MaxPricePerKg && request.MaxPricePerKg.HasValue)
            return ServiceResult<DemandResponse>.Invalid("Giá tối thiểu không được lớn hơn giá tối đa.");
        var factory = await CurrentFactory(userId, ct);
        var demand = new FactoryDemand
        {
            FactoryId = factory.Id, MaterialType = request.MaterialType.ToString(),
            RequiredWeightKg = request.QuantityKg, MinPricePerKg = request.MinPricePerKg,
            MaxPricePerKg = request.MaxPricePerKg, Deadline = request.Deadline ?? DateTime.UtcNow.AddDays(7),
            IsActive = request.IsActive, Note = request.Note?.Trim(), UpdatedAt = DateTime.UtcNow
        };
        Db.FactoryDemands.Add(demand);
        await Db.SaveChangesAsync(ct);
        return ServiceResult<DemandResponse>.Success(data: DemandView(demand), resourceId: demand.Id);
    }

    public async Task<ServiceResult<DemandResponse>> UpdateAsync(Guid userId, Guid id, DemandRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var demand = await Db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (demand is null) return ServiceResult<DemandResponse>.NotFound("Không tìm thấy nhu cầu.");
        demand.MaterialType = request.MaterialType.ToString(); demand.RequiredWeightKg = request.QuantityKg;
        demand.MinPricePerKg = request.MinPricePerKg; demand.MaxPricePerKg = request.MaxPricePerKg;
        demand.Deadline = request.Deadline ?? DateTime.UtcNow.AddDays(7); demand.IsActive = request.IsActive;
        demand.Note = request.Note?.Trim();
        demand.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return ServiceResult<DemandResponse>.Success(data: DemandView(demand));
    }

    public async Task<ServiceResult<DemandResponse>> ToggleAsync(Guid userId, Guid id, DemandStatusRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var demand = await Db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (demand is null) return ServiceResult<DemandResponse>.NotFound("Không tìm thấy nhu cầu.");
        demand.IsActive = request.IsActive;
        demand.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return ServiceResult<DemandResponse>.Success(data: DemandView(demand));
    }

    public async Task<ServiceResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var demand = await Db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (demand is null) return ServiceResult.NotFound("Không tìm thấy nhu cầu.");
        if (await Db.InventoryBatches.AnyAsync(b => b.SourceDemandId == id, ct))
            return ServiceResult.Conflict("Nhu cầu đã có lô gửi tới; hãy ngừng nhận thay vì xóa để giữ lịch sử giao dịch.");
        Db.FactoryDemands.Remove(demand);
        try { await Db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23503" })
        {
            Db.Entry(demand).State = EntityState.Unchanged;
            return ServiceResult.Conflict("Nhu cầu vừa có lô gửi tới; hãy ngừng nhận thay vì xóa.");
        }
        return ServiceResult.Success(message: "Đã xóa nhu cầu.");
    }

    private static DemandResponse DemandView(FactoryDemand x) => new DemandResponse
    {
        Id = x.Id,
        MaterialType = x.MaterialType,
        QuantityKg = x.RequiredWeightKg,
        MinPricePerKg = x.MinPricePerKg,
        MaxPricePerKg = x.MaxPricePerKg,
        Deadline = x.Deadline,
        IsActive = x.IsActive,
        Note = x.Note,
        CreatedAt = x.CreatedAt
    };
}
