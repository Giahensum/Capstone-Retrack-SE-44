using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Employee;
using Xunit;

namespace Retrack.API.Tests;

public sealed class EmployeeDispatchPostgresTests
{
    [PostgresPickupFact]
    public async Task BootstrapUpgradeAndDanangSeedRunTwiceAndDispatchWithoutChangingCompletedOrders()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        Assert.Equal("Retrack_TV2_test",new NpgsqlConnectionStringBuilder(connection).Database);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName,"db","bootstrap"))) root = root.Parent;
        Assert.NotNull(root);
        var schema = "dispatch_seed_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"CREATE SCHEMA {schema}",admin)) await cmd.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
        try
        {
            await using var sql = new NpgsqlConnection(scoped); await sql.OpenAsync();
            async Task Run(string file)
            {
                await using var cmd = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root!.FullName,file)),sql);
                await cmd.ExecuteNonQueryAsync();
            }
            await Run("db/bootstrap/retrack-system.sql");
            await Run("db/bootstrap/retrack-system.sql");
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
            var depotId=Guid.Parse("fa100000-0000-4000-8000-000000000001");
            var otherDepotId=Guid.Parse("fa100000-0000-4000-8000-000000000002");
            var demoDate=new DateOnly(2026,12,7);var now=PickupSchedule.At(demoDate,7);
            var clock=new DispatchTestClock(now);
            EmployeeDispatchService Dispatch(AppDbContext c)=>new(new Retrack.API.Repositories.EmployeeDispatchRepository(c),new Retrack.API.Repositories.StaffUnitOfWork(c),clock);
            var fixtureOrders=db.PickupRequests.Where(p=>p.TargetDepotId==depotId || p.TargetDepotId==otherDepotId);
            Assert.Equal(160,await fixtureOrders.CountAsync());
            Assert.Equal(160,await fixtureOrders.Select(p=>new{p.Latitude,p.Longitude}).Distinct().CountAsync());
            Assert.Equal(20,await fixtureOrders.Select(p=>p.SellerId).Distinct().CountAsync());
            Assert.Equal(10,await db.Users.CountAsync(u=>u.Role=="DEPOT_EMPLOYEE"));
            Assert.Equal(10,await db.Users.CountAsync(u=>u.Role=="DEPOT_EMPLOYEE" && u.IsActive));
            Assert.Equal(40,await fixtureOrders.CountAsync(p=>p.PreferredDatetime==null && p.CollectionDate==demoDate));
            foreach(var hour in new[]{7,9,11,13,15,17}) Assert.Equal(20,await fixtureOrders.CountAsync(p=>p.PreferredDatetime==PickupSchedule.At(demoDate,hour) && p.PreferredEndDatetime==PickupSchedule.At(demoDate,hour+2)));
            Assert.Empty(await db.EmployeeDispatchShifts.ToListAsync());
            var completed=await db.PickupRequests.AsNoTracking().Where(p=>p.Status=="DONE").Select(p=>new{p.Id,p.AcceptedCollectorId,p.GrossAmount,p.NetAmount}).OrderBy(p=>p.Id).ToListAsync();
            async Task Plan(){
                await using var context=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
                await new EmployeeDailyPlanService(new(context),new Retrack.API.Repositories.EmployeeDispatchRepository(context),new Retrack.API.Repositories.StaffUnitOfWork(context)).PrepareAsync(now,default);
            }
            await Task.WhenAll(Plan(),Plan());await Dispatch(db).DispatchAsync(default);
            foreach(var depot in new[]{depotId,otherDepotId}) {
                Assert.Equal(15,await db.EmployeeCollectionAreas.CountAsync(a=>a.DepotId==depot));
                Assert.Equal(5,await db.DepotStaffs.CountAsync(st=>st.DepotId==depot && st.StaffType=="DEPOT_EMPLOYEE" && st.IsActive));
                Assert.Equal(80,await fixtureOrders.CountAsync(p=>p.TargetDepotId==depot && p.Status=="SCHEDULED"));
                var loads=await fixtureOrders.Where(p=>p.TargetDepotId==depot).GroupBy(p=>p.AcceptedCollectorId).Select(g=>g.Count()).ToListAsync();
                Assert.Equal(5,loads.Count);Assert.All(loads,x=>Assert.Equal(16,x));
                var shifts=await db.EmployeeDispatchShifts.Where(sh=>db.EmployeeCollectionAreas.Any(ar=>ar.Id==sh.AreaId && ar.DepotId==depot)).ToListAsync();
                Assert.Equal(5,shifts.Count);Assert.All(shifts,sh=>{Assert.Equal(PickupSchedule.At(demoDate,7),sh.StartsAt);Assert.Equal(PickupSchedule.At(demoDate,19),sh.EndsAt);});
            }
            Assert.False(await fixtureOrders.AnyAsync(p=>!db.DepotStaffs.Any(st=>st.UserId==p.AcceptedCollectorId && st.DepotId==p.TargetDepotId && st.IsActive)));
            var eventCount=await db.EmployeeDispatchEvents.CountAsync();await Run("db/bootstrap/retrack-system.sql");await Dispatch(db).DispatchAsync(default);
            Assert.Equal(eventCount,await db.EmployeeDispatchEvents.CountAsync());
            Assert.Equal(completed,await db.PickupRequests.AsNoTracking().Where(p=>p.Status=="DONE").Select(p=>new{p.Id,p.AcceptedCollectorId,p.GrossAmount,p.NetAmount}).OrderBy(p=>p.Id).ToListAsync());
            // Tất cả ca cùng kho tạm dừng: đơn chờ đúng kho, không bị giao sang kho còn người.
            var firstDepotShifts=await db.EmployeeDispatchShifts.AsNoTracking().Where(sh=>db.EmployeeCollectionAreas.Any(ar=>ar.Id==sh.AreaId && ar.DepotId==depotId)).ToListAsync();
            foreach(var sh in firstDepotShifts) await Dispatch(db).ChangeAvailabilityAsync(sh.EmployeeId,sh.Id,new("FULL","Về kho bàn giao phế liệu"),default);
            await Dispatch(db).DispatchAsync(default);
            Assert.Equal(80,await fixtureOrders.CountAsync(p=>p.TargetDepotId==depotId && p.AcceptedCollectorId==null && !p.RequiresOwnerReview));
            Assert.Equal(80,await fixtureOrders.CountAsync(p=>p.TargetDepotId==otherDepotId && p.Status=="SCHEDULED"));
            var resumed=firstDepotShifts[0];await Dispatch(db).ChangeAvailabilityAsync(resumed.EmployeeId,resumed.Id,new("AVAILABLE",null),default);await Dispatch(db).DispatchAsync(default);
            Assert.True(await fixtureOrders.AnyAsync(p=>p.TargetDepotId==depotId && p.AcceptedCollectorId==resumed.EmployeeId));
            // Hết giờ: đơn chưa bắt đầu trả về Owner và worker không tự nhặt lại.
            var expiredService=new EmployeeDispatchService(new Retrack.API.Repositories.EmployeeDispatchRepository(db),new Retrack.API.Repositories.StaffUnitOfWork(db),new DispatchTestClock(PickupSchedule.At(demoDate,20)));
            await expiredService.DispatchAsync(default);await expiredService.DispatchAsync(default);
            Assert.Equal(160,await fixtureOrders.CountAsync(p=>p.RequiresOwnerReview && p.AcceptedCollectorId==null));
            // Owner xác nhận ngày mới, ghi lịch sử và cho phép quay lại điều phối đúng ngày.
            var owner=await db.Depots.Where(d=>d.Id==depotId).Select(d=>d.OwnerId).SingleAsync();
            var ownerService=new Retrack.API.Services.Depot.DepotDispatchService(new Retrack.API.Repositories.DepotDispatchRepository(db),
                new Retrack.API.Services.Depot.DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db),new Retrack.API.Repositories.DepotPaymentReadRepository(db)),new Retrack.API.Repositories.DepotUnitOfWork(db),clock);
            var pickup=await fixtureOrders.Where(p=>p.TargetDepotId==depotId).Select(p=>p.Id).FirstAsync();
            var date=demoDate.AddDays(1);
            var input=new Retrack.API.DTOs.Depot.DispatchRescheduleInput(Guid.NewGuid(),date,null,null,"Đã gọi người bán và thống nhất ngày mới");
            await ownerService.RescheduleAsync(owner,depotId,pickup,input);await ownerService.RescheduleAsync(owner,depotId,pickup,input);
            var rescheduled=await db.PickupRequests.AsNoTracking().SingleAsync(p=>p.Id==pickup);
            Assert.False(rescheduled.RequiresOwnerReview);Assert.Equal(date,rescheduled.CollectionDate);Assert.Null(rescheduled.AcceptedCollectorId);
            // Người rảnh chưa có ca vẫn được Owner chọn, tạo ca và phân công nguyên tử.
            var reserve=new User {Role="DEPOT_EMPLOYEE",Email="reserve@test.invalid",IsActive=true};
            db.DepotStaffs.Add(new(){User=reserve,DepotId=depotId,StaffType="DEPOT_EMPLOYEE",IsActive=true});await db.SaveChangesAsync();
            await ownerService.RescheduleAsync(owner,depotId,pickup,new(Guid.NewGuid(),demoDate,null,null,"Người bán đồng ý thu gom trong chiều nay"));
            var manual=new Retrack.API.DTOs.Depot.DispatchAssignInput(Guid.NewGuid(),Guid.Empty,"Bố trí nhân viên dự phòng theo thỏa thuận",reserve.Id);
            await ownerService.AssignAsync(owner,depotId,pickup,manual);await ownerService.AssignAsync(owner,depotId,pickup,manual);
            Assert.Equal(reserve.Id,(await db.PickupRequests.AsNoTracking().SingleAsync(p=>p.Id==pickup)).AcceptedCollectorId);
            Assert.Single(await db.EmployeeDispatchShifts.Where(sh=>sh.EmployeeId==reserve.Id).ToListAsync());
            var crossDepotEmployee=await db.DepotStaffs.Where(st=>st.DepotId==otherDepotId && st.StaffType=="DEPOT_EMPLOYEE").Select(st=>st.UserId).FirstAsync();
            await Assert.ThrowsAsync<ArgumentException>(()=>ownerService.AssignAsync(owner,depotId,pickup,new(Guid.NewGuid(),Guid.Empty,"Không được giao sang nhân viên kho khác",crossDepotEmployee)));


        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [PostgresPickupFact]
    public async Task ConcurrentDispatchAndPauseKeepOneAssignmentAndDoNotMoveCheckedInOrder()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("Retrack_TV2_test", parsed.Database);
        var schema = "dispatch_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
        AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
        try
        {
            await using var setup = NewDb();
            await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
            User UserFor(string role) => new() { Email = $"{Guid.NewGuid():N}@test.local", Role = role };
            var first = UserFor("DEPOT_EMPLOYEE"); var second = UserFor("DEPOT_EMPLOYEE");
            var depot = new Depot { Owner = UserFor("DEPOT_OWNER"), VerificationStatus = "APPROVED" };
            setup.DepotStaffs.AddRange(new DepotStaff { User = first, Depot = depot, StaffType = first.Role },
                new DepotStaff { User = second, Depot = depot, StaffType = second.Role });
            var area = new EmployeeCollectionArea { DepotId = depot.Id, Name = "Test", Latitude = 16.05, Longitude = 108.22 };
            setup.EmployeeCollectionAreas.Add(area);
            var shift = new EmployeeDispatchShift { AreaId = area.Id, EmployeeId = first.Id,
                StartsAt = DateTime.UtcNow.AddMinutes(-1), EndsAt = DateTime.UtcNow.AddHours(1) };
            setup.EmployeeDispatchShifts.Add(shift);
            var pickup = new PickupRequest { Seller = UserFor("SELLER"), TargetDepot = depot,
                Address = "Test", Latitude = 16.05m, Longitude = 108.22m };
            setup.PickupRequests.Add(pickup);
            await setup.SaveChangesAsync();
            async Task Dispatch() { await using var db = NewDb(); await StaffTestServices.Dispatch(db).DispatchAsync(default); }
            await Task.WhenAll(Dispatch(), Dispatch());
            Assert.Single(await setup.EmployeeDispatchEvents.ToListAsync());
            Assert.Equal(2,await setup.Notifications.CountAsync());
            Assert.Equal(first.Id,(await setup.PickupRequests.AsNoTracking().SingleAsync()).AcceptedCollectorId);
            setup.EmployeeDispatchShifts.Add(new EmployeeDispatchShift { AreaId = area.Id, EmployeeId = second.Id,
                StartsAt = shift.StartsAt, EndsAt = shift.EndsAt });
            await setup.SaveChangesAsync();
            async Task Pause()
            {
                await using var db = NewDb();
                await StaffTestServices.Dispatch(db).ChangeAvailabilityAsync(first.Id, shift.Id,
                    new("FULL","Xe đầy cần về kho bàn giao"),default);
            }
            await Task.WhenAll(Pause(), Dispatch());
            Assert.Equal(second.Id,(await setup.PickupRequests.AsNoTracking().SingleAsync()).AcceptedCollectorId);
            Assert.Equal(2,await setup.EmployeeDispatchEvents.CountAsync());
            await using var proofDb = NewDb();
            await StaffTestServices.Collection(proofDb,new EmployeeCollectionTests.TestImages())
                .CheckInAsync(second.Id,pickup.Id,new() {
                    PhotoTakenAt = DateTimeOffset.UtcNow,
                    ImageFile = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(new byte[] {255,216,255,224,0,0,0,0}),
                        0,8,"imageFile","proof.jpg")
                },default);
            var secondShift = await setup.EmployeeDispatchShifts.AsNoTracking().SingleAsync(s => s.EmployeeId == second.Id);
            await using var pauseDb = NewDb();
            await StaffTestServices.Dispatch(pauseDb).ChangeAvailabilityAsync(second.Id,secondShift.Id,
                new("STOPPED","Không thể tiếp tục ca thu gom"),default);
            var final = await setup.PickupRequests.AsNoTracking().SingleAsync();
            Assert.Equal(second.Id,final.AcceptedCollectorId); Assert.Equal("IN_PROGRESS",final.Status);
            Assert.Equal(2,await setup.EmployeeDispatchEvents.CountAsync());
        }
        finally
        {
            // Chỉ xóa schema fixture do ca kiểm thử này tạo trong DB kiểm thử đã xác minh tên.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
