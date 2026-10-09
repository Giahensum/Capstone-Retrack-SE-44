using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Retrack.API.Data;
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
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "db", "bootstrap", "01-retrack-system.sql")))
            directory = directory.Parent;
        if (directory == null) throw new FileNotFoundException("Không tìm thấy SQL tổng hợp.");
        var sql = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "db", "bootstrap", "01-retrack-system.sql"));

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

    [Fact]
    public async Task UnifiedSqlAndDemoSeedInitializeUsableDataForEveryRole()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Cần RETRACK_TEST_CONNECTION.");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Chỉ chạy trên Retrack_TV2_test.");

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "db", "bootstrap", "01-retrack-system.sql")))
            directory = directory.Parent;
        if (directory == null) throw new FileNotFoundException("Không tìm thấy SQL tổng hợp.");
        var schemaSql = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "db", "bootstrap", "01-retrack-system.sql"));
        var seedSql = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "db", "bootstrap", "02-seed-data.sql"));

        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        var schema = $"bootstrap_test_{Guid.NewGuid():N}";
        await using (var setup = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"; SET LOCAL search_path TO \"{schema}\", public;", db, tx))
            await setup.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand(schemaSql, db, tx) { CommandTimeout = 120 })
            await create.ExecuteNonQueryAsync();
        // Giữ test trong transaction/schema cô lập; bỏ wrapper giao dịch của file seed
        // để COMMIT trong fixture không thoát khỏi transaction rollback của test.
        var isolatedSeedSql = seedSql
            .Replace("BEGIN;", "", StringComparison.OrdinalIgnoreCase)
            .Replace("COMMIT;", "", StringComparison.OrdinalIgnoreCase);
        await using (var seed = new NpgsqlCommand(isolatedSeedSql, db, tx) { CommandTimeout = 120 })
            await seed.ExecuteNonQueryAsync();
        await using (var repeatSeed = new NpgsqlCommand(isolatedSeedSql, db, tx) { CommandTimeout = 120 })
            await repeatSeed.ExecuteNonQueryAsync();

        await AssertModelColumnsAsync(db, tx, schema);

        await using (var verify = new NpgsqlCommand("""
            SELECT
                (SELECT COUNT(*) FROM users WHERE role IN ('ADMIN','SELLER','DEPOT_OWNER','DEPOT_EMPLOYEE','DRIVER','FACTORY')),
                (SELECT COUNT(*) FROM depots),
                (SELECT COUNT(*) FROM factories),
                (SELECT COUNT(*) FROM depot_staffs WHERE is_active),
                (SELECT COUNT(*) FROM pickup_requests),
                (SELECT COUNT(*) FROM pickup_request_items),
                (SELECT COUNT(*) FROM inventory_batches WHERE status = 'PENDING_APPROVAL' AND direct_offer_factory_id IS NOT NULL),
                (SELECT COUNT(*) FROM factory_depot_partnerships WHERE status = 'PENDING'),
                (SELECT COUNT(*) FROM market_prices),
                (SELECT COUNT(*) FROM users WHERE email = 'depot@retrack.vn' AND full_name = 'Ngô Sỹ Giá'),
                (SELECT password_hash FROM users WHERE email = 'depot@retrack.vn'),
                (SELECT COUNT(*) FROM inventory_batches WHERE status = 'MARKETPLACE'),
                (SELECT COUNT(*) FROM transport_jobs),
                (SELECT COUNT(*) FROM notifications WHERE transport_job_id IS NOT NULL)
            """, db, tx))
        {
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(10L, reader.GetInt64(0));
            Assert.Equal(3L, reader.GetInt64(1));
            Assert.Equal(3L, reader.GetInt64(2));
            Assert.Equal(6L, reader.GetInt64(3));
            Assert.Equal(9L, reader.GetInt64(4));
            Assert.Equal(4L, reader.GetInt64(5));
            Assert.Equal(2L, reader.GetInt64(6));
            Assert.Equal(2L, reader.GetInt64(7));
            Assert.Equal(8L, reader.GetInt64(8));
            Assert.Equal(1L, reader.GetInt64(9));
            var depotHash = reader.GetString(10);
            Assert.StartsWith("$2a$", depotHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("Depot@123", depotHash));
            Assert.Equal(2L, reader.GetInt64(11));
            Assert.Equal(1L, reader.GetInt64(12));
            Assert.Equal(1L, reader.GetInt64(13));
        }

        await tx.RollbackAsync();
    }

    private static async Task AssertColumnsAsync(NpgsqlConnection db, NpgsqlTransaction tx, string schema)
    {
        await using var columns = new NpgsqlCommand("""
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = @schema AND
              ((table_name = 'depots' AND column_name IN ('contact_phone', 'tax_code', 'description'))
                OR (table_name = 'users' AND column_name IN ('bank_name', 'bank_account_number', 'bank_account_name', 'bank_qr_url'))
                OR (table_name = 'inventory_batches' AND column_name = 'image_urls')
                OR (table_name = 'pickup_checkins' AND column_name = 'revision'))
            """, db, tx);
        columns.Parameters.AddWithValue("schema", schema);
        Assert.Equal(9L, (long)(await columns.ExecuteScalarAsync())!);
    }

    private static async Task AssertModelColumnsAsync(NpgsqlConnection db, NpgsqlTransaction tx, string schema)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db).Options;
        await using var context = new AppDbContext(options);
        await context.Database.UseTransactionAsync(tx);

        var actualColumns = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("""
            SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = @schema
            """, db, tx))
        {
            command.Parameters.AddWithValue("schema", schema);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) actualColumns.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        var missingColumns = new List<string>();
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is null) continue;
            var table = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName(table);
                if (columnName is not null && !actualColumns.Contains($"{tableName}.{columnName}"))
                    missingColumns.Add($"{tableName}.{columnName}");
            }
        }

        Assert.Empty(missingColumns);
    }
}
