using Microsoft.EntityFrameworkCore.Storage;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class StaffUnitOfWork(AppDbContext db) : IStaffUnitOfWork
{
    public async Task<IStaffTransaction> BeginAsync(CancellationToken ct) =>
        new Transaction(await db.Database.BeginTransactionAsync(ct));
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public void ClearTracking() => db.ChangeTracker.Clear();

    private sealed class Transaction(IDbContextTransaction transaction) : IStaffTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
