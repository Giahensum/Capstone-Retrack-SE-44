using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Controllers.Employee;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public sealed class PostgresPickupFactAttribute : FactAttribute
{
    public PostgresPickupFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")))
            Skip = "Set RETRACK_TEST_POSTGRES to run the isolated PostgreSQL concurrency test.";
    }
}

public class EmployeePickupPostgresTests
{
    [PostgresPickupFact]
    public async Task ConcurrentEmployeesProduceOneWinnerAndOneNotificationOnPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        // All tables/data belong to a unique test schema; existing application tables are untouched.
        var schema = "pickup_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false }.ConnectionString;
        AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
        try
        {
            await using var setup = NewDb();
            await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
            User UserFor(string role) => new() { Role = role, Email = Guid.NewGuid().ToString("N") + "@test.local" };
            var first = UserFor("DEPOT_EMPLOYEE"); var second = UserFor("DEPOT_EMPLOYEE");
            var seller = UserFor("SELLER");
            var depot = new Depot { Name = "Test", Owner = UserFor("DEPOT_OWNER") };
            setup.DepotStaffs.AddRange(new DepotStaff { User = first, Depot = depot, StaffType = first.Role },
                new DepotStaff { User = second, Depot = depot, StaffType = second.Role });
            var pickup = new PickupRequest { Seller = seller, TargetDepot = depot, Address = "Test address" };
            setup.PickupRequests.Add(pickup);
            await setup.SaveChangesAsync();

            EmployeePickupController Controller(AppDbContext context, Guid userId) => new(context, new NotificationService(context))
            {
                ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "DEPOT_EMPLOYEE") }, "test")) } }
            };
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<IActionResult> Accept(Guid userId)
            {
                await using var context = NewDb();
                await start.Task;
                return await Controller(context, userId).Accept(pickup.Id, default);
            }
            var a = Accept(first.Id); var b = Accept(second.Id);
            start.SetResult();
            var results = await Task.WhenAll(a, b);
            Assert.Single(results.OfType<OkObjectResult>());
            Assert.Single(results.OfType<ConflictObjectResult>());
            await setup.Entry(pickup).ReloadAsync();
            Assert.Equal("SCHEDULED", pickup.Status);
            Assert.Contains(pickup.AcceptedCollectorId, new Guid?[] { first.Id, second.Id });
            var notification = Assert.Single(await setup.Notifications.ToListAsync());
            Assert.Equal(seller.Id, notification.UserId);
            var dashboard = Assert.IsType<OkObjectResult>(await Controller(setup, pickup.AcceptedCollectorId!.Value).Dashboard(default));
            Assert.Equal(pickup.Id, Assert.IsType<ApiResponse<EmployeeDashboardDto>>(dashboard.Value).Data!.ActivePickup!.Id);
            var pool = Assert.IsType<OkObjectResult>(await Controller(setup, first.Id).Pool(default));
            Assert.Empty(Assert.IsType<ApiResponse<List<PickupPoolItemDto>>>(pool.Value).Data!);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
