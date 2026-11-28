using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryPartnershipReadRepository(AppDbContext db) : IFactoryPartnershipReadRepository
{
    public async Task<FactoryPartnershipPage> ListAsync(Guid factoryId, PageQuery query, CancellationToken ct)
    {
        IQueryable<FactoryDepotPartnership> source = db.FactoryDepotPartnerships.AsNoTracking().Where(x => x.FactoryId == factoryId)
            .Include(x => x.Depot).ThenInclude(x => x.Owner);
        if (!string.IsNullOrWhiteSpace(query.PartnerStatus))
            source = query.PartnerStatus == "BLOCKED"
                ? source.Where(x => x.Status == "BLOCKED" || x.BlockedByDepot || x.BlockedByFactory)
                : source.Where(x => x.Status == query.PartnerStatus && !x.BlockedByDepot && !x.BlockedByFactory);
        var total = await source.CountAsync(ct);
        var partnerships = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var depotIds = partnerships.Select(x => x.DepotId).ToArray();
        var reviewStats = depotIds.Length == 0
            ? new Dictionary<Guid, PartnerReviewStats>()
            : await db.FactoryDepotReviews.AsNoTracking()
                .Where(x => x.FactoryId == factoryId && depotIds.Contains(x.DepotId))
                .GroupBy(x => x.DepotId)
                .Select(group => new PartnerReviewStats(
                    group.Key,
                    group.Count(),
                    group.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => x.Rating).FirstOrDefault(),
                    group.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => x.Comment).FirstOrDefault()))
                .ToDictionaryAsync(x => x.DepotId, ct);
        var orderStats = depotIds.Length == 0
            ? new Dictionary<Guid, PartnerOrderStats>()
            : await db.InventoryBatches.AsNoTracking()
                .Where(batch => batch.TargetFactoryId == factoryId && depotIds.Contains(batch.DepotId) && batch.Status != "MARKETPLACE")
                .GroupBy(batch => batch.DepotId)
                .Select(group => new PartnerOrderStats(
                    group.Key,
                    group.Count(),
                    group.Count(batch => batch.Status == "PAID" || (batch.Status == "COMPLETED" && batch.SettlementWorkflowVersion == 0))))
                .ToDictionaryAsync(x => x.DepotId, ct);
        var items = partnerships.Select(x =>
        {
            reviewStats.TryGetValue(x.DepotId, out var review);
            orderStats.TryGetValue(x.DepotId, out var orders);
            return new PartnerResponse
            {
                Id = x.Id, DepotId = x.DepotId, Name = x.Depot.Name, Address = x.Depot.Address,
                ContactPhone = x.Depot.Owner.Phone,
                Status = x.EffectiveStatus, BlockedByFactory = x.BlockedByFactory, BlockedByDepot = x.BlockedByDepot,
                LegacyBlocked = x.Status == "BLOCKED", Rating = x.Depot.Rating,
                LatestRating = review?.LatestRating, LatestComment = review?.LatestComment,
                ReviewCount = review?.ReviewCount ?? 0, OrderCount = orders?.OrderCount ?? 0,
                CompletedOrderCount = orders?.CompletedOrderCount ?? 0,
                ContractCode = x.ContractCode, ContractVersion = x.ContractVersion, ContractTerms = x.ContractTerms,
                ContractTermsHash = x.ContractTermsHash, ContractStatus = x.ContractStatus,
                FactorySignedAt = x.FactorySignedAt, DepotSignedAt = x.DepotSignedAt,
                ContractEffectiveAt = x.ContractEffectiveAt, ContractExpiresAt = x.ContractExpiresAt,
                TerminationStatus = x.TerminationStatus, TerminationRequestedBy = x.TerminationRequestedBy,
                TerminationRequestedAt = x.TerminationRequestedAt, TerminationReason = x.TerminationReason, TerminatedAt = x.TerminatedAt
            };
        }).ToList();
        return new FactoryPartnershipPage(items, total);
    }

    private sealed record PartnerReviewStats(Guid DepotId, int ReviewCount, int? LatestRating, string? LatestComment);
    private sealed record PartnerOrderStats(Guid DepotId, int OrderCount, int CompletedOrderCount);
}
