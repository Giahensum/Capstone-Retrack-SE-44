using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryOrderReadRepository(AppDbContext db) : IFactoryOrderReadRepository
{
    private IQueryable<InventoryBatch> ForFactory(Guid factoryId) => db.InventoryBatches.AsNoTracking()
        .Where(x => x.TargetFactoryId == factoryId && x.Status != "MARKETPLACE" && x.Status != "DRAFT")
        .Include(x => x.Depot).ThenInclude(x => x.Owner)
        .Include(x => x.TransportJob)
        .Include(x => x.QualityCheck);

    public async Task<FactoryOrderPage> ListAsync(Guid factoryId, OrderQuery query, string? status, CancellationToken ct)
    {
        var source = ForFactory(factoryId);
        if (query.Stage == "qc")
            source = source.Where(x => x.Status == "RECEIVED" || x.Status == "WEIGHED" || x.Status == "VERIFIED" || x.Status == "REJECTED" || x.Status == "NEGOTIATING" || x.Status == "RETURN_REQUESTED" || x.Status == "RETURNING" || x.Status == "RETURNED" ||
                x.Status == "DELIVERED" || ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP" || x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && x.TransportJob != null && x.TransportJob.Status == "DELIVERED"));
        else if (query.Stage == "settlements")
            source = source.Where(x => x.Status == "VERIFIED" || x.Status == "NEGOTIATING" || x.Status == "SETTLEMENT_PENDING" || x.Status == "SETTLEMENT_ACCEPTED" || x.Status == "SETTLEMENT_DISPUTED" || x.Status == "COMPLETED" || x.Status == "PAID");
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.Id.ToString().Contains(search) || (x.Code != null && x.Code.Contains(search)) || x.Depot.Name.Contains(search));
        }
        source = status switch
        {
            "PENDING_APPROVAL" => source.Where(x => x.Status == "PENDING_APPROVAL" || x.Status == "PENDING_FACTORY"),
            "ACCEPTED" => source.Where(x =>
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") &&
                 (x.TransportJob == null || (x.TransportJob.Status != "PICKED_UP" && x.TransportJob.Status != "IN_TRANSIT" && x.TransportJob.Status != "ON_THE_WAY" && x.TransportJob.Status != "IN_PROGRESS" && x.TransportJob.Status != "DELIVERED")))),
            "IN_TRANSIT" => source.Where(x => ((x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && (x.TransportJob == null || x.TransportJob.Status != "DELIVERED")) ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") && x.TransportJob != null &&
                 (x.TransportJob.Status == "PICKED_UP" || x.TransportJob.Status == "IN_TRANSIT" || x.TransportJob.Status == "ON_THE_WAY" || x.TransportJob.Status == "IN_PROGRESS"))),
            "DELIVERED" => source.Where(x => x.Status == "DELIVERED" ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP" || x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && x.TransportJob != null && x.TransportJob.Status == "DELIVERED")),
            "PAID" => source.Where(x => x.Status == "PAID"),
            "PAYMENT_REPORTED" => source.Where(x => x.Status == "COMPLETED" && x.QualityCheck != null && x.QualityCheck.PaymentProofUrl != null),
            "SETTLEMENT_RECORDED" => source.Where(x => x.Status == "COMPLETED" && (x.QualityCheck == null || x.QualityCheck.PaymentProofUrl == null)),
            "REJECTED" => source.Where(x => x.Status == "REJECTED" || x.Status == "CANCELLED"),
            "RECEIVED" or "WEIGHED" or "VERIFIED" or "NEGOTIATING" or "RETURN_REQUESTED" or "RETURNING" or "RETURNED" or "SETTLEMENT_PENDING" or "SETTLEMENT_ACCEPTED" or "SETTLEMENT_DISPUTED" => source.Where(x => x.Status == status),
            _ => source
        };
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new FactoryOrderPage(rows, total);
    }

    public Task<InventoryBatch?> GetAsync(Guid factoryId, Guid id, CancellationToken ct) =>
        ForFactory(factoryId).SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<InventoryBatch>> DashboardPriorityAsync(Guid factoryId, CancellationToken ct) =>
        await ForFactory(factoryId).Where(x => x.Status == "RECEIVED" || x.Status == "WEIGHED" || x.Status == "DELIVERED" || x.Status == "VERIFIED" ||
                x.Status == "NEGOTIATING" || x.Status == "RETURN_REQUESTED" || x.Status == "RETURNING" ||
                (x.Status == "REJECTED" && x.QualityCheck != null) ||
                x.Status == "SETTLEMENT_PENDING" || x.Status == "SETTLEMENT_DISPUTED" || x.Status == "SETTLEMENT_ACCEPTED" ||
                (x.Status == "COMPLETED" && x.SettlementWorkflowVersion > 0) ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") && x.TransportJob != null && x.TransportJob.Status == "DELIVERED"))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(10).ToListAsync(ct);

    public async Task<IReadOnlyList<InventoryBatch>> DashboardRecentAsync(Guid factoryId, CancellationToken ct) =>
        await ForFactory(factoryId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(5).ToListAsync(ct);
}
