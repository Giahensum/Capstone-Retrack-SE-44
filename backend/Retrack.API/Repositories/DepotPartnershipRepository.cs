using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DepotPartnershipRepository(AppDbContext db) : IDepotPartnershipRepository
{
    public Task<List<DepotPartnershipDto>> ListAsync(Guid depotId, CancellationToken ct) =>
        db.FactoryDepotPartnerships.AsNoTracking().Where(x => x.DepotId == depotId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new DepotPartnershipDto(x.Id, x.FactoryId, x.Factory.Name, x.Factory.Owner!.Phone, x.Status, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
    public Task<FactoryDepotPartnership?> FindAsync(Guid depotId, Guid factoryId, CancellationToken ct) =>
        db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.DepotId == depotId && x.FactoryId == factoryId, ct);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
