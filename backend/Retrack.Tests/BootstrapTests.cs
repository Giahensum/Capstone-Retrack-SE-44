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
        for (var i = 0; i < 2; i++)
        { await using var run = new NpgsqlCommand(sql, connection, tx); await run.ExecuteNonQueryAsync(); }
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(tx);
        Assert.Equal(demo ? 6 : 0, await db.Users.Select(x => x.Role).Distinct().CountAsync());
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
