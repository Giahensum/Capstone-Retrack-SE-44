using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Xunit;
namespace Retrack.Tests;

public sealed class BootstrapTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Full_sql_bootstraps_fresh_schema_is_repeatable_and_matches_runtime_model(bool demo)
    {
        var cs = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")!;
        var builder = new NpgsqlConnectionStringBuilder(cs);
        if (builder.Database != "Retrack_TV2_test") throw new InvalidOperationException("Sai DB test");
        var schema = "bootstrap_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(cs); await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        // Schema UUID chỉ tồn tại trong transaction này, rollback tự dọn; không drop/reset DB.
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}; SET LOCAL search_path TO {schema};", connection, tx)) await create.ExecuteNonQueryAsync();
        var file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql"));
        var sql = (await File.ReadAllTextAsync(file)).Replace("BEGIN;", "").Replace("COMMIT;", "").Replace("SET LOCAL retrack.seed_demo = 'on';", $"SET LOCAL retrack.seed_demo = '{(demo ? "on" : "off")}';");
        DateTime? before = null;
        for (var i = 0; i < 2; i++)
        {
            await using var run = new NpgsqlCommand(sql, connection, tx); await run.ExecuteNonQueryAsync();
            if (i == 0)
            {
                await using var customFee = new NpgsqlCommand("UPDATE system_configs SET config_value='1.00' WHERE config_key='PLATFORM_FEE_PERCENTAGE'; SELECT MIN(updated_at) FROM pickup_requests WHERE status='DONE'", connection, tx);
                var value = await customFee.ExecuteScalarAsync(); before = value is DateTime date ? date : null;
            }
        }
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(tx);
        Assert.Equal(demo ? 6 : 0, await db.Users.Select(x => x.Role).Distinct().CountAsync());
        Assert.Equal("1.00", await db.SystemConfigs.Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE").Select(x => x.ConfigValue).SingleAsync());
        if (before != null) Assert.Equal(before, await db.PickupRequests.Where(x => x.Status == "DONE").MinAsync(x => (DateTime?)x.UpdatedAt));
        // SELECT tất cả cột của từng bảng được runtime map; bắt cột thiếu mà COUNT(*) không phát hiện.
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName(); if (table == null) continue;
            var columns = entity.GetProperties().Select(x => '"' + x.GetColumnName().Replace("\"", "\"\"") + '"').Distinct();
            await using var check = new NpgsqlCommand($"SELECT {string.Join(",", columns)} FROM \"{table.Replace("\"", "\"\"")}\" LIMIT 0", connection, tx);
            await check.ExecuteNonQueryAsync();
        }
        await tx.RollbackAsync();
    }
}
