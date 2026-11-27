using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Xunit;
namespace Retrack.API.Tests;

public sealed class DefenseDemoPostgresTests
{
    [PostgresPickupFact]
    public async Task DatePatchIsScopedRepeatableAndPreservesStartedCompletedAndForeignData()
    {
        var connection=Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        Assert.Equal("Retrack_TV2_test",new NpgsqlConnectionStringBuilder(connection).Database);
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root!=null && !Directory.Exists(Path.Combine(root.FullName,"db","bootstrap")))root=root.Parent;
        Assert.NotNull(root);
        var patchPath=Path.Combine(root!.FullName,"db","employee","prepare_defense_demo.sql");
        Assert.True(File.Exists(patchPath),"Cần commit file SQL tác vụ ngày demo cùng bài kiểm thử.");
        var bootstrap=await File.ReadAllTextAsync(Path.Combine(root.FullName,"db","bootstrap","retrack-system.sql"));
        var patch=await File.ReadAllTextAsync(patchPath);
        var schema="defense_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await create.ExecuteNonQueryAsync();
        var scoped=new NpgsqlConnectionStringBuilder(connection){SearchPath=schema,Pooling=false}.ConnectionString;
        async Task Execute(string sql){await using var c=new NpgsqlConnection(scoped);await c.OpenAsync();await using var cmd=new NpgsqlCommand(sql,c){CommandTimeout=120};await cmd.ExecuteNonQueryAsync();}
        try {
            await Execute(bootstrap);
            // Mô phỏng DB đang dùng bộ 100 đơn cũ: tác vụ phải bổ sung đủ 60 đơn mới.
            await Execute("DELETE FROM pickup_requests WHERE id BETWEEN 'fa500000-0000-4000-8000-000000000101' AND 'fa500000-0000-4000-8000-000000000160';");
            await Execute("""
                UPDATE pickup_requests SET collection_date=collection_date-10,preferred_datetime=preferred_datetime-interval '10 days',preferred_end_datetime=preferred_end_datetime-interval '10 days'
                WHERE id BETWEEN 'fa500000-0000-4000-8000-000000000001' AND 'fa500000-0000-4000-8000-000000000100';
                UPDATE pickup_requests SET status='IN_PROGRESS',checkin_image_url='https://example.com/protected.jpg' WHERE id='fa500000-0000-4000-8000-000000000001';
                UPDATE pickup_requests SET status='DONE',gross_amount=800,net_amount=700 WHERE id='fa500000-0000-4000-8000-000000000002';
                UPDATE users SET full_name='Tên trùng' WHERE email IN ('employee.dispatch01@retrack.test','employee.dispatch02@retrack.test');
                """);
            await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
            var foreignId=Guid.Parse("00000000-0000-0000-0000-000000001002");
            var foreignBefore=await db.PickupRequests.AsNoTracking().Where(p=>p.Id==foreignId).Select(p=>new{p.Status,p.CollectionDate,p.GrossAmount,p.NetAmount,p.AcceptedCollectorId}).SingleAsync();
            await Execute(patch);
            var newDay=new DateOnly(2026,12,7);
            Assert.Equal(158,await db.PickupRequests.CountAsync(p=>p.Id.ToString().StartsWith("fa500000-") && p.CollectionDate==newDay));
            Assert.Equal(38,await db.PickupRequests.CountAsync(p=>p.Id.ToString().StartsWith("fa500000-") && p.CollectionDate==newDay && p.PreferredDatetime==null));
            foreach(var hour in new[]{7,9,11,13,15,17}) {
                var start=new DateTime(2026,12,7,hour-7,0,0,DateTimeKind.Utc);
                Assert.Equal(20,await db.PickupRequests.CountAsync(p=>p.Id.ToString().StartsWith("fa500000-") && p.CollectionDate==newDay && p.PreferredDatetime==start && p.PreferredEndDatetime==start.AddHours(2)));
            }

            var protectedOne=await db.PickupRequests.AsNoTracking().SingleAsync(p=>p.Id==Guid.Parse("fa500000-0000-4000-8000-000000000001"));
            Assert.Equal("IN_PROGRESS",protectedOne.Status);Assert.Equal(new DateOnly(2026,11,27),protectedOne.CollectionDate);Assert.Equal("https://example.com/protected.jpg",protectedOne.CheckinImageUrl);
            var done=await db.PickupRequests.AsNoTracking().SingleAsync(p=>p.Id==Guid.Parse("fa500000-0000-4000-8000-000000000002"));
            Assert.Equal("DONE",done.Status);Assert.Equal(800,done.GrossAmount);Assert.Equal(700,done.NetAmount);
            Assert.Equal(10,await db.Users.Where(u=>u.Role=="DEPOT_EMPLOYEE").Select(u=>u.FullName).Distinct().CountAsync());
            var count=await db.EmployeeDispatchEvents.CountAsync();
            await Execute("UPDATE pickup_requests SET status='SCHEDULED',accepted_collector_id='00000000-0000-0000-0000-000000000004' WHERE id='fa500000-0000-4000-8000-000000000031';");
            await Execute(patch);
            Assert.Equal(count,await db.EmployeeDispatchEvents.CountAsync());
            Assert.Equal("SCHEDULED",await db.PickupRequests.Where(p=>p.Id==Guid.Parse("fa500000-0000-4000-8000-000000000031")).Select(p=>p.Status).SingleAsync());
            await Execute(patch.Replace("SET LOCAL retrack.demo_date = '2026-12-07';","SET LOCAL retrack.demo_date = '2026-12-08';"));
            Assert.Equal(158,await db.PickupRequests.CountAsync(p=>p.Id.ToString().StartsWith("fa500000-") && p.CollectionDate==newDay.AddDays(1)));
            Assert.Equal(foreignBefore,await db.PickupRequests.AsNoTracking().Where(p=>p.Id==foreignId).Select(p=>new{p.Status,p.CollectionDate,p.GrossAmount,p.NetAmount,p.AcceptedCollectorId}).SingleAsync());
            await Execute("UPDATE pickup_requests SET target_depot_id='00000000-0000-0000-0000-000000000010' WHERE id='fa500000-0000-4000-8000-000000000100';");
            await Assert.ThrowsAsync<PostgresException>(()=>Execute(patch));
            Assert.Equal(158,await db.PickupRequests.CountAsync(p=>p.Id.ToString().StartsWith("fa500000-") && p.CollectionDate==newDay.AddDays(1)));
        } finally {
            await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
