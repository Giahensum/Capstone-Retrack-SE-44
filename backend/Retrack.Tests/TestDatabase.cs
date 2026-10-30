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
            // Chỉ chuẩn bị database kiểm thử riêng bằng schema DB-first, không chạy seed demo.
            var connection = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            var postgres = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
            if (connection.Database != "Retrack_TV2_test" || string.IsNullOrWhiteSpace(postgres)
                || new Npgsql.NpgsqlConnectionStringBuilder(postgres).Database != "Retrack_TV2_test")
                throw new InvalidOperationException("Cần cả hai biến kiểm thử trỏ tới Retrack_TV2_test.");
            var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql"));
            var schema = await File.ReadAllTextAsync(schemaPath);
            var schemaEnd = schema.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal);
            if (schemaEnd < 0) throw new InvalidOperationException("Không tìm thấy điểm kết thúc schema để kiểm thử.");
            await db.Database.OpenConnectionAsync();
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = schema[..schemaEnd].Replace("SET LOCAL retrack.seed_demo = 'on';", "SET LOCAL retrack.seed_demo = 'off';") + "\nCOMMIT;";
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync();
            }
            finally { await db.Database.CloseConnectionAsync(); }
            initialized = true;
        }
        finally { MigrationLock.Release(); }
    }
}
