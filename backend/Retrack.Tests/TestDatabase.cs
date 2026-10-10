using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.Tests;

internal static class TestDatabase
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    public static async Task MigrateAsync(AppDbContext db)
    {
        await MigrationLock.WaitAsync();
        try
        {
            await db.Database.MigrateAsync();
            // Các kiểm thử dùng migration lịch sử; đồng bộ cột DB-first mới có trong SQL tổng hợp.
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_name VARCHAR(255);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_account_number VARCHAR(100);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_account_name VARCHAR(255);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_qr_url VARCHAR(2048);
                """);
        }
        finally { MigrationLock.Release(); }
    }
}
