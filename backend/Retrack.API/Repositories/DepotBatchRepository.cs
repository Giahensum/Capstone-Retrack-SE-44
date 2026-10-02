using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;

public sealed class DepotBatchRepository(AppDbContext db) : IDepotBatchRepository
{
    public Task<DepotBatchDetailDto?> DetailAsync(Guid depotId, Guid batchId) => db.InventoryBatches.AsNoTracking()
        .Where(b => b.DepotId == depotId && b.Id == batchId)
        .Select(b => new DepotBatchDetailDto(
            new DepotBatchDto(b.Id, b.DepotId, b.MaterialType, b.DeclaredWeightKg, b.Description, b.Status,
                b.TargetFactoryId ?? b.DirectOfferFactoryId, b.TargetFactory != null ? b.TargetFactory.Name : b.DirectOfferFactory != null ? b.DirectOfferFactory.Name : null, b.CreatedAt, b.Code, b.ImageUrls),
            b.TransportJob == null ? null : b.TransportJob.Status,
            b.TransportJob == null || b.TransportJob.Driver == null ? null : b.TransportJob.Driver.FullName,
            b.FactoryReceivedAt, b.FactoryDecidedAt, b.RejectionReason,
            b.QualityCheck == null ? null : new DepotBatchQualityDto(b.QualityCheck.ActualWeightKg,
                b.QualityCheck.GrossWeightKg, b.QualityCheck.TareWeightKg, b.QualityCheck.DifferencePercentage,
                b.QualityCheck.Grade, b.QualityCheck.Grade == "PENDING" ? null : b.QualityCheck.IsAccepted,
                b.QualityCheck.PurityPercent, b.QualityCheck.MoisturePercent, b.QualityCheck.ContaminationPercent,
                b.QualityCheck.QualityNote, b.QualityCheck.Resolution, b.QualityCheck.TicketNumber,
                b.QualityCheck.TicketImageUrl, b.QualityCheck.InvoiceNumber, b.QualityCheck.InvoiceFileUrl),
            b.SettledAt == null ? null : new DepotBatchSettlementDto(b.AgreedPricePerKg, b.GrossAmount,
                b.PlatformFeeAmount, b.NetAmount, b.PaymentReference, b.SettledAt.Value,
                b.QualityCheck == null ? null : b.QualityCheck.PaymentProofUrl)))
        .SingleOrDefaultAsync();
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
                    b.Description, b.Status, b.TargetFactoryId ?? b.DirectOfferFactoryId, b.TargetFactory == null ? b.DirectOfferFactory == null ? null : b.DirectOfferFactory.Name : b.TargetFactory.Name, b.CreatedAt, b.Code, b.ImageUrls)).ToListAsync() };
    }


    public Task LockDepotAsync(Guid depotId) => db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync();
    public Task<InventoryBatch?> FindOperationAsync(Guid id) => db.InventoryBatches.AsNoTracking().Include(b => b.TargetFactory).Include(b => b.DirectOfferFactory).SingleOrDefaultAsync(b => b.Id == id);
    public Task<Factory?> FindActiveFactoryAsync(Guid id) => db.Factories.SingleOrDefaultAsync(f => f.Id == id && f.Owner != null && f.Owner.IsActive);
    public Task<FactoryDepotPartnership?> FindPartnerAsync(Guid depotId, Guid factoryId) => db.FactoryDepotPartnerships.AsNoTracking().SingleOrDefaultAsync(p => p.DepotId == depotId && p.FactoryId == factoryId);
    public void AddPartner(FactoryDepotPartnership partner) => db.FactoryDepotPartnerships.Add(partner);
    public Task<long> NextNumberAsync() => db.Database.SqlQueryRaw<long>("SELECT nextval('depot_batch_number') AS \"Value\"").SingleAsync();
    public void AddBatch(InventoryBatch batch) => db.InventoryBatches.Add(batch);
    public void AddTransport(TransportJob job) => db.TransportJobs.Add(job);
    public async Task<InventoryBatch?> LockBatchAsync(Guid id)
    {
        var batch = await db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (batch != null) { await db.Entry(batch).ReloadAsync(); await db.Entry(batch).Reference(b => b.QualityCheck).LoadAsync(); }
        return batch;
    }
    public Task<bool> HasTransportAsync(Guid batchId) => db.TransportJobs.AnyAsync(t => t.BatchId == batchId);
}
