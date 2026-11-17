using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public sealed class ClosedPeriodBillingPostgresTests
{
    [PostgresPickupFact]
    public async Task ConcurrentClosedPeriodReconciliationCreatesOneSupplementAndUpgradeCanRunTwice()
    {
        var source=Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var other=Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
        var verified=new NpgsqlConnectionStringBuilder(source);
        Assert.Equal("Retrack_TV2_test",verified.Database);
        Assert.Equal("Retrack_TV2_test",new NpgsqlConnectionStringBuilder(other).Database);
        Assert.Contains(verified.Host,new[]{"localhost","127.0.0.1"});
        var schema="billing_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(source);await admin.OpenAsync();
        await using(var command=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await command.ExecuteNonQueryAsync();
        var connection=new NpgsqlConnectionStringBuilder(source){SearchPath=schema+",public",Pooling=false}.ConnectionString;
        try
        {
            var sql=await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../db/bootstrap/retrack-system.sql")));
            var schemaSql=sql[..sql.IndexOf("-- RETRACK_SCHEMA_END",StringComparison.Ordinal)]+"\nCOMMIT;";
            await using(var setup=new NpgsqlConnection(connection))
            {
                await setup.OpenAsync();
                await using(var command=new NpgsqlCommand(schemaSql,setup){CommandTimeout=120})await command.ExecuteNonQueryAsync();
                // Giả lập index unique ba cột do EF phiên bản cũ sinh ra.
                await using(var oldIndex=new NpgsqlCommand("CREATE UNIQUE INDEX legacy_invoice_period ON platform_invoices(payer_id,period_year,period_month)",setup))await oldIndex.ExecuteNonQueryAsync();
                await using(var command=new NpgsqlCommand(schemaSql,setup){CommandTimeout=120})await command.ExecuteNonQueryAsync();
            }
            AppDbContext Db()=>new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
            AdminService Service(AppDbContext db)=>new(new UserRepository(db),new MarketPriceRepository(db),new AuditLogRepository(db),new PlatformInvoiceRepository(db),new NotificationService(db),db);
            var payer=new User{Role="DEPOT_OWNER",FullName="Kho đối soát",Email=Guid.NewGuid()+"@test.invalid"};
            var actor=new User{Role="ADMIN",FullName="Admin test",Email=Guid.NewGuid()+"@test.invalid"};
            PlatformTransaction Fee(decimal amount)=>new(){SourceType="PICKUP_REQUEST",SourceId=Guid.NewGuid(),PayerId=payer.Id,FeeAmount=amount,CreatedAt=new(2026,1,15,0,0,0,DateTimeKind.Utc)};
            await using(var db=Db())
            {
                db.AddRange(payer,actor);db.PlatformTransactions.Add(Fee(50));await db.SaveChangesAsync();
                await Service(db).GenerateMonthlyInvoicesAsync(2026,1,actor.Id);
                var original=await db.PlatformInvoices.SingleAsync();original.Status="PAID";original.PaidAt=DateTime.UtcNow;
                db.PlatformTransactions.Add(Fee(100));await db.SaveChangesAsync();
            }
            async Task Issue(){await using var db=Db();await Service(db).GenerateMonthlyInvoicesAsync(2026,1,actor.Id);}
            await Task.WhenAll(Issue(),Issue());
            await using(var db=Db())
            {
                var rows=await db.PlatformInvoices.OrderBy(i=>i.Revision).ToListAsync();
                Assert.Equal(2,rows.Count);Assert.Equal("PAID",rows[0].Status);Assert.Equal(50,rows[0].TotalFeeAmount);
                Assert.Equal(rows[0].Id,rows[1].AdjustmentOfId);Assert.Equal(1,rows[1].Revision);Assert.Equal(100,rows[1].TotalFeeAmount);
                Assert.Single(await db.AuditLogs.Where(a=>a.Action=="SUPPLEMENT").ToListAsync());
                db.PlatformTransactions.Add(Fee(25));await db.SaveChangesAsync();
                await Service(db).GenerateMonthlyInvoicesAsync(2026,1,actor.Id);
                Assert.Equal(125,rows[1].TotalFeeAmount);Assert.Equal(2,await db.PlatformInvoices.CountAsync());
            }
        }
        finally
        {
            // Chỉ dọn schema fixture vừa tạo, không dọn dữ liệu DB dùng chung.
            await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
