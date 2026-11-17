using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
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
    public async Task GeneratingWhileAdminConfirmsPaymentCannotDeadlockOnNotificationForeignKey()
    {
        var source=Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var verified=new NpgsqlConnectionStringBuilder(source);
        Assert.Equal("Retrack_TV2_test",verified.Database);
        Assert.Equal("Retrack_TV2_test",new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")).Database);
        Assert.Contains(verified.Host,new[]{"localhost","127.0.0.1"});
        var schema="billing_locks_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(source);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await create.ExecuteNonQueryAsync();
        var connection=new NpgsqlConnectionStringBuilder(source){SearchPath=schema+",public",Pooling=false}.ConnectionString;
        try
        {
            var sql=await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../db/bootstrap/retrack-system.sql")));
            await using(var setup=new NpgsqlConnection(connection))
            {
                await setup.OpenAsync();
                await using var command=new NpgsqlCommand(sql[..sql.IndexOf("-- RETRACK_SCHEMA_END",StringComparison.Ordinal)]+"\nCOMMIT;",setup){CommandTimeout=120};
                await command.ExecuteNonQueryAsync();
            }
            AppDbContext Db(DbCommandInterceptor? interceptor=null)
            {
                var options=new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection);
                if(interceptor!=null)options.AddInterceptors(interceptor);
                return new(options.Options);
            }
            AdminService Service(AppDbContext db)=>new(new UserRepository(db),new MarketPriceRepository(db),new AuditLogRepository(db),new PlatformInvoiceRepository(db),new NotificationService(db),db);
            var payer=new User{Role="DEPOT_OWNER",FullName="Kho đồng thời",Email=Guid.NewGuid()+"@test.invalid"};
            var actor=new User{Role="ADMIN",FullName="Admin đồng thời",Email=Guid.NewGuid()+"@test.invalid"};
            var invoice=new PlatformInvoice{Payer=payer,PeriodYear=2026,PeriodMonth=1,TotalFeeAmount=50};
            await using(var seed=Db())
            {
                seed.AddRange(actor,invoice);
                seed.PlatformTransactions.Add(new(){SourceType="PICKUP_REQUEST",SourceId=Guid.NewGuid(),PayerId=payer.Id,FeeAmount=50,CreatedAt=new(2026,1,15,0,0,0,DateTimeKind.Utc)});
                await seed.SaveChangesAsync();
            }
            var invoiceLocked=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var payerLocked=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Ép đúng thứ tự: Admin giữ invoice, generate giữ payer, rồi Admin chèn thông báo.
            var confirm=Task.Run(async()=>
            {
                await using var db=Db(new LockBoundary("FROM platform_invoices",invoiceLocked,payerLocked.Task));
                return await Service(db).MarkInvoicePaidAsync(invoice.Id,actor.Id);
            });
            await invoiceLocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var generate=Task.Run(async()=>
            {
                await using var db=Db(new LockBoundary("FROM users",payerLocked,Task.CompletedTask));
                return await Service(db).GenerateMonthlyInvoicesAsync(2026,1,actor.Id);
            });
            await Task.WhenAll(confirm,generate).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("PAID",(await confirm).Status);Assert.Empty(await generate);
            await using var check=Db();
            Assert.Equal(50,(await check.PlatformInvoices.SingleAsync()).TotalFeeAmount);
            Assert.Single(await check.Notifications.Where(n=>n.UserId==payer.Id).ToListAsync());
        }
        finally
        {
            await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class LockBoundary(string table,TaskCompletionSource<bool> reached,Task continueAfter) : DbCommandInterceptor
    {
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default)
        {
            if(command.CommandText.Contains(table,StringComparison.Ordinal)&&command.CommandText.Contains("FOR ",StringComparison.Ordinal))
            {
                reached.TrySetResult(true);
                await continueAfter.WaitAsync(TimeSpan.FromSeconds(10),cancellationToken);
            }
            return result;
        }
    }

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
