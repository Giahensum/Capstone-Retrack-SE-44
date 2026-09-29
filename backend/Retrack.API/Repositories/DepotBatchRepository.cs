using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;

public sealed class DepotBatchRepository(AppDbContext db) : IDepotBatchRepository
{
    public async Task<PagedResult<DepotBatchDto>> ListAsync(Guid depotId, DepotQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new ArgumentException("Phân trang không hợp lệ.");
        var source = db.InventoryBatches.AsNoTracking().Where(b => b.DepotId == depotId);
        if (!string.IsNullOrEmpty(query.Status))
        {
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


    public Task LockDepotAsync(Guid depotId) => db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync();
    public Task<InventoryBatch?> FindOperationAsync(Guid id) => db.InventoryBatches.AsNoTracking().Include(b => b.TargetFactory).SingleOrDefaultAsync(b => b.Id == id);
    public Task<Factory?> FindActiveFactoryAsync(Guid id) => db.Factories.SingleOrDefaultAsync(f => f.Id == id && f.Owner != null && f.Owner.IsActive);
    public Task<FactoryDepotPartnership?> FindPartnerAsync(Guid depotId, Guid factoryId) => db.FactoryDepotPartnerships.SingleOrDefaultAsync(p => p.DepotId == depotId && p.FactoryId == factoryId);
    public void AddPartner(FactoryDepotPartnership partner) => db.FactoryDepotPartnerships.Add(partner);
    public Task<long> NextNumberAsync() => db.Database.SqlQueryRaw<long>("SELECT nextval('depot_batch_number') AS \"Value\"").SingleAsync();
    public void AddBatch(InventoryBatch batch) => db.InventoryBatches.Add(batch);
    public void AddTransport(TransportJob job) => db.TransportJobs.Add(job);
    public Task<InventoryBatch?> LockBatchAsync(Guid id) => db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();
    public Task<bool> HasTransportAsync(Guid batchId) => db.TransportJobs.AnyAsync(t => t.BatchId == batchId);
}
