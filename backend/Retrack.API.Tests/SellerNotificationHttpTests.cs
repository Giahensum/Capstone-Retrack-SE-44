using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
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
using Retrack.API.Controllers.Seller;
using Retrack.API.Data;
using Retrack.API.Models;
using Xunit;

namespace Retrack.API.Tests;

// Host HTTP chỉ tồn tại trong test: xác thực giả lập, policy SELLER và model binding thật.
// Không đăng ký handler này trong ứng dụng và không coi đây là kiểm thử chữ ký JWT.
public sealed class SellerNotificationHttpTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid seller = Guid.NewGuid();
    private readonly Guid notice = Guid.NewGuid();
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddControllers().AddApplicationPart(typeof(SellerNotificationController).Assembly);
        builder.Services.AddAuthentication("NotificationTest").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("NotificationTest", _ => { });
        builder.Services.AddAuthorization();
        app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new User { Id = seller, Role = "SELLER", Email = $"http-{seller:N}@test.invalid" });
            db.Notifications.Add(new Notification { Id = notice, UserId = seller, Title = "Thông báo kiểm thử HTTP" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (app != null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }

    private async Task<HttpResponseMessage> Send(string verb, string path, string? role = "SELLER")
    {
        using var request = new HttpRequestMessage(new HttpMethod(verb), $"/api/seller/notifications{path}");
        if (role != null)
        {
            request.Headers.Add("X-Fixture-Role", role);
            request.Headers.Add("X-Fixture-User", seller.ToString());
        }
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("GET", "")] [InlineData("GET", "/unread-count")]
    [InlineData("PATCH", "/{id}/read")] [InlineData("PATCH", "/{id}/unread")] [InlineData("PATCH", "/read-all")]
    public async Task Every_endpoint_challenges_anonymous_forbids_other_roles_and_accepts_seller(string verb, string template)
    {
        var path = template.Replace("{id}", notice.ToString());
        using var anonymous = await Send(verb, path, null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var role in new[] { "DEPOT_OWNER", "DEPOT_EMPLOYEE", "DRIVER", "FACTORY", "ADMIN" })
        {
            using var forbidden = await Send(verb, path, role);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        using var allowed = await Send(verb, path);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Theory]
    [InlineData("/not-a-guid/read")] [InlineData("/not-a-guid/unread")]
    public async Task Invalid_guid_does_not_match_mutation_route(string path)
    {
        using var response = await Send("PATCH", path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.SingleAsync()).IsRead);
    }

    [Theory]
    [InlineData("?page=abc")] [InlineData("?pageSize=abc")] [InlineData("?unreadOnly=abc")]
    [InlineData("?page=2147483648")] [InlineData("?pageSize=2147483648")]
    public async Task Invalid_query_binding_returns_400(string query)
    {
        using var response = await Send("GET", query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Fixture-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Fixture-User"].ToString()), new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
