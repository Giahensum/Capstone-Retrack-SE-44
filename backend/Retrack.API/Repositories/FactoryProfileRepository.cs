using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryProfileRepository(AppDbContext db) : IFactoryProfileRepository
{
    public Task<Retrack.API.Models.Factory?> FindByOwnerAsync(Guid ownerId, CancellationToken ct) =>
        db.Factories.SingleOrDefaultAsync(x => x.OwnerId == ownerId, ct);

    public void Add(Retrack.API.Models.Factory factory) => db.Factories.Add(factory);

    public Task<User?> FindOwnerAsync(Guid ownerId, CancellationToken ct) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ownerId, ct);
}
