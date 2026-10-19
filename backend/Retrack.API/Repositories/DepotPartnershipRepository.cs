using Retrack.API.DTOs;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DepotPartnershipRepository(AppDbContext db) : IDepotPartnershipRepository
{
    public async Task<PagedResult<DepotPartnershipDto>> ListAsync(Guid depotId, DepotQuery query, CancellationToken ct)
    {
        var source = db.FactoryDepotPartnerships.AsNoTracking().Where(x => x.DepotId == depotId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new DepotPartnershipDto(x.Id, x.FactoryId, x.Factory.Name, x.Factory.Owner!.Phone,
                x.Status == "BLOCKED" || x.BlockedByDepot || x.BlockedByFactory ? "BLOCKED" : x.Status, x.CreatedAt, x.UpdatedAt,
                x.BlockedByDepot, x.BlockedByFactory, x.Status == "BLOCKED",
                x.ContractCode, x.ContractVersion, x.ContractTerms, x.ContractTermsHash, x.ContractStatus,
                x.FactorySignedAt, x.DepotSignedAt, x.ContractEffectiveAt, x.ContractExpiresAt,
                x.TerminationStatus, x.TerminationRequestedBy, x.TerminationRequestedAt, x.TerminationReason, x.TerminatedAt));
        return await DepotRepositoryPage.ReadAsync(source, query, ct);
    }
    public async Task<FactoryDepotPartnership?> FindAsync(Guid depotId, Guid factoryId, CancellationToken ct)
    {
        var partner = await db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.DepotId == depotId && x.FactoryId == factoryId, ct);
        if (partner != null) await db.Entry(partner).ReloadAsync(ct);
        return partner;
    }
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public Task LockDepotAsync(Guid depotId, CancellationToken ct) => db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
    public Task<bool> HasOpenOrdersAsync(Guid depotId, Guid factoryId, CancellationToken ct) =>
        db.InventoryBatches.AnyAsync(x => x.DepotId == depotId && x.TargetFactoryId == factoryId &&
            x.Status != "COMPLETED" && x.Status != "PAID" && x.Status != "REJECTED" && x.Status != "CANCELLED", ct);
}
