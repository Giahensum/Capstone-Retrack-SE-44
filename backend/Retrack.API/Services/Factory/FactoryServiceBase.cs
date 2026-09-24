using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Factory;

public abstract class FactoryServiceBase(AppDbContext db)
{
    protected AppDbContext Db { get; } = db;

    protected async Task<Retrack.API.Models.Factory> CurrentFactory(Guid userId, CancellationToken ct = default)
    {
        var factory = await Db.Factories.SingleOrDefaultAsync(x => x.OwnerId == userId, ct);
        if (factory is not null) return factory;

        var owner = await Db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (owner is null || !string.Equals(owner.Role, "FACTORY", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Tài khoản chưa có hồ sơ nhà máy.");

        factory = new Retrack.API.Models.Factory { OwnerId = owner.Id, Name = owner.FullName, Address = "Chưa cập nhật" };
        Db.Factories.Add(factory);
        await Db.SaveChangesAsync(ct);
        return factory;
    }
}
