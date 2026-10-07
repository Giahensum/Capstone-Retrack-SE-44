using Npgsql;
using Xunit;

namespace Retrack.Tests;

public sealed class SchemaScriptTests
{
    [Fact]
    public async Task UnifiedSqlBuildsFreshSchemaAndRepairsKnownExistingDepotColumns()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Cần RETRACK_TEST_CONNECTION.");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Chỉ chạy trên Retrack_TV2_test.");

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "db", "depot ower", "retrack-system.sql")))
            directory = directory.Parent;
        if (directory == null) throw new FileNotFoundException("Không tìm thấy SQL tổng hợp.");
        var sql = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "db", "depot ower", "retrack-system.sql"));

        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        var schema = $"schema_test_{Guid.NewGuid():N}";
        await using (var setup = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"; SET LOCAL search_path TO \"{schema}\", public;", db, tx))
            await setup.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand(sql, db, tx) { CommandTimeout = 120 })
            await create.ExecuteNonQueryAsync();

        await AssertColumnsAsync(db, tx, schema);
        await using (var simulateOldSchema = new NpgsqlCommand("""
            ALTER TABLE depots DROP COLUMN contact_phone;
            ALTER TABLE depots DROP COLUMN tax_code;
            ALTER TABLE depots DROP COLUMN description;
            DROP TABLE pickup_checkins;
            """, db, tx))
            await simulateOldSchema.ExecuteNonQueryAsync();
        await using (var upgrade = new NpgsqlCommand(sql, db, tx) { CommandTimeout = 120 })
            await upgrade.ExecuteNonQueryAsync();
        await AssertColumnsAsync(db, tx, schema);
        await tx.RollbackAsync();
    }

    private static async Task AssertColumnsAsync(NpgsqlConnection db, NpgsqlTransaction tx, string schema)
    {
        await using var columns = new NpgsqlCommand("""
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = @schema AND
              ((table_name = 'depots' AND column_name IN ('contact_phone', 'tax_code', 'description'))
                OR (table_name = 'inventory_batches' AND column_name = 'image_urls')
                OR (table_name = 'pickup_checkins' AND column_name = 'revision'))
            """, db, tx);
        columns.Parameters.AddWithValue("schema", schema);
        Assert.Equal(5L, (long)(await columns.ExecuteScalarAsync())!);
    }
}
