using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.Tests;

internal static class TestDatabase
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    public static async Task MigrateAsync(AppDbContext db)
    {
        await MigrationLock.WaitAsync();
        try { await db.Database.MigrateAsync(); }
        finally { MigrationLock.Release(); }
    }
}
