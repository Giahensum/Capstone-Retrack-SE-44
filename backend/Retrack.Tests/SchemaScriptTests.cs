using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.RegularExpressions;
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
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "db", "bootstrap", "retrack-system.sql")))
            directory = directory.Parent;
        if (directory == null) throw new FileNotFoundException("Không tìm thấy SQL tổng hợp.");
        var sql = WithoutTransaction(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "db", "bootstrap", "retrack-system.sql")));
        sql = sql.Replace("SET LOCAL retrack.seed_demo = 'on';", "SET LOCAL retrack.seed_demo = 'off';");

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
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "db", "bootstrap", "retrack-system.sql")))
            directory = directory.Parent;
        if (directory == null) throw new FileNotFoundException("Không tìm thấy SQL tổng hợp.");
        var sql = WithoutTransaction(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "db", "bootstrap", "retrack-system.sql")));

        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        var schema = $"bootstrap_test_{Guid.NewGuid():N}";
        await using (var setup = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"; SET LOCAL search_path TO \"{schema}\", public;", db, tx))
            await setup.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand(sql, db, tx) { CommandTimeout = 120 })
            await create.ExecuteNonQueryAsync();
        await using (var repeat = new NpgsqlCommand(sql, db, tx) { CommandTimeout = 120 })
            await repeat.ExecuteNonQueryAsync();

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
            Assert.Equal(13L, reader.GetInt64(0));
            Assert.Equal(3L, reader.GetInt64(1));
            Assert.Equal(3L, reader.GetInt64(2));
            Assert.Equal(8L, reader.GetInt64(3));
            Assert.Equal(16L, reader.GetInt64(4));
            Assert.Equal(10L, reader.GetInt64(5));
            Assert.Equal(2L, reader.GetInt64(6));
            Assert.Equal(2L, reader.GetInt64(7));
            Assert.Equal(8L, reader.GetInt64(8));
            Assert.Equal(1L, reader.GetInt64(9));
            var depotHash = reader.GetString(10);
            Assert.StartsWith("$2a$", depotHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("Depot@123", depotHash));
            Assert.Equal(2L, reader.GetInt64(11));
            Assert.Equal(6L, reader.GetInt64(12));
            Assert.Equal(1L, reader.GetInt64(13));
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task UnifiedBootstrapSupportsSchemaOnlyAndRollsBackEntireFailedSetup()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Cần RETRACK_TEST_CONNECTION.");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Chỉ chạy trên Retrack_TV2_test.");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "db", "bootstrap", "retrack-system.sql")))
            directory = directory.Parent;
        if (directory == null) throw new FileNotFoundException("Không tìm thấy SQL tổng hợp.");
        var sql = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "db", "bootstrap", "retrack-system.sql"));
        var schemas = new[] { $"bootstrap_off_{Guid.NewGuid():N}", $"bootstrap_fail_{Guid.NewGuid():N}" };
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        try
        {
            foreach (var schema in schemas)
            {
                await using var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin);
                await create.ExecuteNonQueryAsync();
            }
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schemas[0], Pooling = false };
            await using (var db = new NpgsqlConnection(scoped.ConnectionString))
            {
                await db.OpenAsync();
                await using var command = new NpgsqlCommand(sql.Replace("SET LOCAL retrack.seed_demo = 'on';", "SET LOCAL retrack.seed_demo = 'off';"), db) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync();
                await using var verify = new NpgsqlCommand("SELECT COUNT(*) FROM users", db);
                Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
            }
            scoped.SearchPath = schemas[1];
            await using (var db = new NpgsqlConnection(scoped.ConnectionString))
            {
                await db.OpenAsync();
                // Chạy nguyên wrapper BEGIN/COMMIT; lỗi sau seed phải hủy cả bảng lẫn dữ liệu vừa tạo.
                var failing = Regex.Replace(sql, @"(?m)^COMMIT;\r?$", "SELECT 1 / 0;\nCOMMIT;");
                await using var command = new NpgsqlCommand(failing, db) { CommandTimeout = 120 };
                var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.DivisionByZero, error.SqlState);
                await using (var rollback = new NpgsqlCommand("ROLLBACK;", db)) await rollback.ExecuteNonQueryAsync();
                await using var verify = new NpgsqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @schema", db);
                verify.Parameters.AddWithValue("schema", schemas[1]);
                Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            foreach (var schema in schemas)
            {
                await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
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

    // Giữ giao dịch ngoài của test; không bỏ BEGIN/END bên trong function/DO.
    private static string WithoutTransaction(string sql) => Regex.Replace(sql, @"(?m)^(BEGIN;|COMMIT;)\r?$", "");

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
