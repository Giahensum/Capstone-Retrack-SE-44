using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.Tests;

internal static class TestDatabase
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool initialized;
    public static async Task MigrateAsync(AppDbContext db)
    {
        await MigrationLock.WaitAsync();
        try
        {
            // Chỉ dựng schema trước fixture đầu tiên, không chạy DDL giữa các bài đang truy vấn.
            if (initialized) return;
            await db.Database.MigrateAsync();
            // Các kiểm thử dùng migration lịch sử; đồng bộ cột DB-first mới có trong SQL tổng hợp.
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_name VARCHAR(255);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_account_number VARCHAR(100);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_account_name VARCHAR(255);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS bank_qr_url VARCHAR(2048);
                """);
            var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql"));
            var schema = await File.ReadAllTextAsync(schemaPath);
            var stockStart = schema.IndexOf("-- RETRACK_DEPOT_STOCK_V1", StringComparison.Ordinal);
            var schemaEnd = schema.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal);
            if (stockStart < 0 || schemaEnd <= stockStart) throw new InvalidOperationException("Không tìm thấy khối schema nhập kho để kiểm thử.");
            await db.Database.OpenConnectionAsync();
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = schema[stockStart..schemaEnd];
                await command.ExecuteNonQueryAsync();
            }
            finally { await db.Database.CloseConnectionAsync(); }
            initialized = true;
        }
        finally { MigrationLock.Release(); }
    }
}
