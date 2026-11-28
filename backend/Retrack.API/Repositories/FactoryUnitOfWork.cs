using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryUnitOfWork(AppDbContext db) : IFactoryUnitOfWork
{
    public async Task<IFactoryTransaction> BeginAsync(CancellationToken ct) =>
        db.Database.IsRelational()
            ? new Transaction(await db.Database.BeginTransactionAsync(ct))
            : new NoTransaction();

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed class Transaction(IDbContextTransaction transaction) : IFactoryTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class NoTransaction : IFactoryTransaction
    {
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
