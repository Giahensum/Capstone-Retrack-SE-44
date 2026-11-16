using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Retrack.API.Controllers.Employee;
using Retrack.API.Data;
using Retrack.API.Middleware;
using Retrack.API.Models;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;
using Retrack.API.Services.Staff;
using Xunit;

namespace Retrack.API.Tests;

// Chạy HTTP/model binding/DI thật, dùng xác thực fixture; không kiểm chứng chữ ký JWT.
public sealed class StaffRefactorHttpTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    private HttpClient client = null!;
    private readonly User employee = UserFor("DEPOT_EMPLOYEE"), driver = UserFor("DRIVER");
    private readonly User seller = UserFor("SELLER"), owner = UserFor("DEPOT_OWNER"), factoryOwner = UserFor("FACTORY");
    private Guid pickupId, jobId;
    private static User UserFor(string role) => new() { Role = role, Email = $"{Guid.NewGuid():N}@test.invalid", FullName = role };

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<AppDbContext>(_ => new HttpSqliteContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options));
        builder.Services.AddStaffWorkflows();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddSingleton<ICloudinaryService, EmployeeCollectionTests.TestImages>();
        builder.Services.AddScoped<Retrack.API.Repositories.IStaffProfileRepository, Retrack.API.Repositories.StaffProfileRepository>();
        builder.Services.AddScoped<IStaffProfileService, StaffProfileService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(EmployeePickupController).Assembly);
        builder.Services.AddAuthentication("StaffFixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("StaffFixture", _ => { });
        builder.Services.AddAuthorization();
        app = builder.Build();
        app.UseExceptionHandling(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var depot = new Depot { Owner = owner, Name = "Kho HTTP", VerificationStatus = "APPROVED", Latitude = 16.05m, Longitude = 108.2m };
            var factory = new Factory { Owner = factoryOwner, Name = "Nhà máy HTTP", Latitude = 16.05m, Longitude = 108.2m };
            db.DepotStaffs.AddRange(new DepotStaff { User = employee, Depot = depot, StaffType = "DEPOT_EMPLOYEE" },
                new DepotStaff { User = driver, Depot = depot, StaffType = "DRIVER" });
            var pickup = new PickupRequest { Seller = seller, TargetDepot = depot, Address = "Điểm HTTP", Status = "PENDING", PlatformFeePercentage = 3 };
            var job = new TransportJob { Batch = new InventoryBatch { Depot = depot, TargetFactory = factory,
                Status = "TRANSPORT_READY", MaterialType = "PET", DeclaredWeightKg = 10 } };
            db.PickupRequests.Add(pickup); db.TransportJobs.Add(job);
            await db.SaveChangesAsync(); pickupId = pickup.Id; jobId = job.Id;
        }
        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }
    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (app != null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }
    private async Task<JsonElement> Send(string role, HttpMethod method, string path, HttpContent? content = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add("X-Fixture-Role", role);
        request.Headers.Add("X-Fixture-User", (role == "DRIVER" ? driver.Id : employee.Id).ToString());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{method} {path}: {(int)response.StatusCode} {body}");
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "null" : body).RootElement.Clone();
    }
    private static MultipartFormDataContent Photo(Guid? operationId = null)
    {
        var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent([255, 216, 255, 224, 0, 0, 0, 0]);
        bytes.Headers.ContentType = new("image/jpeg");
        form.Add(bytes, "ImageFile", "test.jpg");
        form.Add(new StringContent(DateTimeOffset.UtcNow.ToString("O")), "PhotoTakenAt");
        if (operationId.HasValue) form.Add(new StringContent(operationId.Value.ToString()), "operationId");
        return form;
    }

    [Fact]
    public async Task Employee_http_workflow_persists_across_requests_and_keeps_contracts()
    {
        var pool = await Send("DEPOT_EMPLOYEE", HttpMethod.Get, "/api/employee/pickup-pool");
        Assert.Equal(JsonValueKind.Array, pool.GetProperty("data").ValueKind);
        Assert.Equal(pickupId, pool.GetProperty("data")[0].GetProperty("id").GetGuid());
        await Send("DEPOT_EMPLOYEE", HttpMethod.Post, $"/api/employee/pickup/{pickupId}/accept");
        await Send("DEPOT_EMPLOYEE", HttpMethod.Post, $"/api/employee/pickup/{pickupId}/accept", expected: HttpStatusCode.Conflict);
        await Send("DEPOT_EMPLOYEE", HttpMethod.Post, $"/api/employee/pickup/{pickupId}/checkin", Photo());
        var saved = await Send("DEPOT_EMPLOYEE", HttpMethod.Put, $"/api/employee/pickup/{pickupId}/classification",
            JsonContent.Create(new { expectedRevision = 0, items = new[] { new { materialType = "PET", weightKg = 5m, pricePerKg = 1000m } } }));
        Assert.Equal(1, saved.GetProperty("data").GetProperty("revision").GetInt32());
        var weighed = await Send("DEPOT_EMPLOYEE", HttpMethod.Post, $"/api/employee/pickup/{pickupId}/submit-weigh", JsonContent.Create(new { expectedRevision = 1 }));
        Assert.Equal("WEIGHED", weighed.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(5000m, weighed.GetProperty("data").GetProperty("grossAmount").GetDecimal());
        Assert.Equal(4850m, weighed.GetProperty("data").GetProperty("netAmount").GetDecimal());
        // Quyết định Seller là điều kiện bên ngoài use case Employee.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.PickupRequests.SingleAsync(p => p.Id == pickupId)).Status = "SELLER_CONFIRMED";
            await db.SaveChangesAsync();
        }
        var result = await Send("DEPOT_EMPLOYEE", HttpMethod.Post, $"/api/employee/pickup/{pickupId}/finalize", JsonContent.Create(new { expectedRevision = 1 }));
        Assert.Equal("AWAITING_PAYMENT", result.GetProperty("data").GetProperty("status").GetString());
        await Send("DEPOT_EMPLOYEE", HttpMethod.Post, $"/api/employee/pickup/{pickupId}/finalize", JsonContent.Create(new { expectedRevision = 1 }));
        using var verify = app.Services.CreateScope();
        var persisted = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotNull((await persisted.PickupRequests.SingleAsync(p => p.Id == pickupId)).StockReceivedAt);
        Assert.Equal(2, await persisted.EmployeeCollectionEvents.CountAsync());
        Assert.Single(await persisted.PickupCheckIns.ToListAsync());
    }

    [Fact]
    public async Task Driver_http_workflow_reloads_evidence_and_retries_without_duplicates()
    {
        await Send("DRIVER", HttpMethod.Get, "/api/driver/job-pool");
        var accepted = await Send("DRIVER", HttpMethod.Post, $"/api/driver/job/{jobId}/accept");
        Assert.Equal("ACCEPTED", accepted.GetProperty("data").GetProperty("status").GetString());
        var checkInId = Guid.NewGuid();
        var acknowledged = await Send("DRIVER", HttpMethod.Post, $"/api/driver/job/{jobId}/checkin", Photo(checkInId));
        Assert.True(acknowledged.GetProperty("data").GetProperty("completed").GetBoolean());
        await Send("DRIVER", HttpMethod.Post, $"/api/driver/job/{jobId}/checkin", Photo(checkInId));
        var startId = Guid.NewGuid();
        await Send("DRIVER", HttpMethod.Post, $"/api/driver/job/{jobId}/start", JsonContent.Create(new { operationId = startId }));
        await Send("DRIVER", HttpMethod.Post, $"/api/driver/job/{jobId}/start", JsonContent.Create(new { operationId = startId }));
        await Send("DRIVER", HttpMethod.Post, $"/api/driver/job/{jobId}/checkout", Photo(Guid.NewGuid()));
        var snapshot = await Send("DRIVER", HttpMethod.Get, $"/api/driver/job/{jobId}/delivery");
        Assert.Equal("DELIVERED", snapshot.GetProperty("data").GetProperty("job").GetProperty("status").GetString());
        Assert.Equal(3, snapshot.GetProperty("data").GetProperty("events").GetArrayLength());
        await Send("DRIVER", HttpMethod.Post, $"/api/driver/job/{jobId}/incident", JsonContent.Create(new { operationId = Guid.NewGuid(), reason = "Sự cố sau khi đã giao" }), HttpStatusCode.Conflict);
        using var verify = app.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("DELIVERED", (await db.TransportJobs.Include(j => j.Batch).SingleAsync()).Batch.Status);
        Assert.Equal(3, await db.DriverDeliveryEvents.CountAsync());
    }

    [Theory]
    [InlineData("DEPOT_EMPLOYEE", "/api/employee/dashboard")]
    [InlineData("DEPOT_EMPLOYEE", "/api/employee/pickup-pool")]
    [InlineData("DEPOT_EMPLOYEE", "/api/employee/pickups/active")]
    [InlineData("DEPOT_EMPLOYEE", "/api/employee/pickup-history")]
    [InlineData("DEPOT_EMPLOYEE", "/api/employee/stats")]
    [InlineData("DEPOT_EMPLOYEE", "/api/employee/notifications")]
    [InlineData("DEPOT_EMPLOYEE", "/api/employee/dispatch/shifts")]
    [InlineData("DRIVER", "/api/driver/dashboard")]
    [InlineData("DRIVER", "/api/driver/job-pool")]
    [InlineData("DRIVER", "/api/driver/jobs/mine")]
    [InlineData("DRIVER", "/api/driver/trip-history")]
    [InlineData("DRIVER", "/api/driver/stats")]
    [InlineData("DRIVER", "/api/driver/notifications")]
    [InlineData("DRIVER", "/api/driver/transport")]
    public async Task Read_endpoints_resolve_production_DI_and_preserve_role_guards(string role, string path)
    {
        using var anonymous = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await Send(role == "DRIVER" ? "DEPOT_EMPLOYEE" : "DRIVER", HttpMethod.Get, path, expected: HttpStatusCode.Forbidden);
        await Send(role, HttpMethod.Get, path);
    }

    [Fact]
    public void Controllers_and_workflow_services_have_no_EF_dependency()
    {
        var assembly = typeof(EmployeePickupController).Assembly;
        var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract
            && t.Namespace is "Retrack.API.Controllers.Employee" or "Retrack.API.Controllers.Driver"
                or "Retrack.API.Services.Employee" or "Retrack.API.Services.Driver");
        foreach (var type in types)
        foreach (var parameter in type.GetConstructors().SelectMany(c => c.GetParameters()))
            Assert.False(parameter.ParameterType == typeof(AppDbContext)
                || parameter.ParameterType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true, type.FullName);
    }

    // SQLite 8 không dịch SUM(decimal); chỉ fixture HTTP đổi hai cột tổng hợp sang provider double.
    // Nghiệp vụ vẫn dùng decimal và schema PostgreSQL thật không bị thay đổi.
    private sealed class HttpSqliteContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PickupRequest>().Property(p => p.GrossAmount).HasConversion<double>();
            modelBuilder.Entity<PickupRequestItem>().Property(p => p.WeightKg).HasConversion<double>();
        }
    }

    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Fixture-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Fixture-User"].ToString()),
                new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
