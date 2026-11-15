using Microsoft.EntityFrameworkCore.Storage;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DepotUnitOfWork(AppDbContext db) : IDepotUnitOfWork
{
    public async Task<IDepotTransaction> BeginAsync() => new Transaction(await db.Database.BeginTransactionAsync());
    public async Task SaveAsync() => await db.SaveChangesAsync();

    private sealed class Transaction(IDbContextTransaction transaction) : IDepotTransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
