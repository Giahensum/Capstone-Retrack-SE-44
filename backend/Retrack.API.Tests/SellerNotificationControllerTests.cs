using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Controllers.Seller;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

// Gọi trực tiếp controller với SQLite trong bộ nhớ để kiểm tra được cả ExecuteUpdate.
// Bộ này không thay thế kiểm thử middleware JWT/HTTP và khóa dòng PostgreSQL.
public sealed class SellerNotificationControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly User seller = new() { Email = $"notice-{Guid.NewGuid():N}@test.invalid", Role = "SELLER" };
    private readonly User other = new() { Email = $"notice-{Guid.NewGuid():N}@test.invalid", Role = "SELLER" };
    private readonly DateTime now = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => now.ToString("O"));
        db = Open();
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(seller, other);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private SellerNotificationController Controller(string? userId = null) => new(db)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId ?? seller.Id.ToString()), new Claim(ClaimTypes.Role, "SELLER")], "test"))
            }
        }
    };

    private async Task Seed()
    {
        db.Notifications.AddRange(
            new Notification { Id = Id(1), UserId = seller.Id, Title = "Hệ thống", Message = null, CreatedAt = now.AddHours(-3) },
            new Notification { Id = Id(2), UserId = seller.Id, Title = "Thu gom", Category = "PICKUP", IsRead = true, CreatedAt = now.AddHours(-2) },
            new Notification { Id = Id(3), UserId = seller.Id, Title = "Thanh toán", Category = "PAYMENT", CreatedAt = now.AddHours(-1) },
            new Notification { Id = Id(4), UserId = seller.Id, Title = "Đã đăng lô", Category = "LIQUIDATION", TargetType = "LIQUIDATION", TargetId = Id(100), CreatedAt = now },
            new Notification { Id = Id(5), UserId = seller.Id, Title = "Đã hủy lô", Category = "LIQUIDATION", TargetType = "LIQUIDATION", TargetId = Id(100), IsRead = true, CreatedAt = now },
            new Notification { Id = Id(6), UserId = other.Id, Title = "Riêng tư chưa đọc", Category = "LIQUIDATION", CreatedAt = now.AddHours(1) },
            new Notification { Id = Id(7), UserId = other.Id, Title = "Riêng tư đã đọc", IsRead = true, CreatedAt = now.AddHours(2) });
        await db.SaveChangesAsync();
    }

    private static ApiResponse<T> Ok<T>(IActionResult result)
    {
        var response = Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(response.Success);
        return response;
    }

    private static Task<IActionResult> Invoke(SellerNotificationController controller, string action, CancellationToken ct = default) => action switch
    {
        "list" => controller.List(ct: ct),
        "count" => controller.UnreadCount(ct),
        "read" => controller.MarkRead(Id(4), ct),
        "unread" => controller.MarkUnread(Id(5), ct),
        "all" => controller.ReadAll(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    [Fact]
    public async Task List_returns_only_own_notifications_newest_first_with_stable_ties_and_no_entity_graph()
    {
        await Seed();
        var data = Ok<PagedResult<SellerNotificationDto>>(await Controller().List()).Data!;
        Assert.Equal(1, data.Page); Assert.Equal(20, data.PageSize); Assert.Equal(5, data.TotalCount);
        Assert.Equal(new[] { Id(4), Id(5), Id(3), Id(2), Id(1) }, data.Items.Select(n => n.Id));
        Assert.Equal(string.Empty, data.Items.Single(n => n.Id == Id(1)).Message);
        Assert.Equal(Id(100), data.Items[0].TargetId); Assert.Equal("LIQUIDATION", data.Items[0].TargetType);
        var json = JsonSerializer.SerializeToElement(data.Items[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "category", "createdAt", "id", "isRead", "message", "pickupRequestId", "targetId", "targetType", "title" },
            json.EnumerateObject().Select(p => p.Name).OrderBy(x => x));
    }

    [Theory]
    [InlineData(null, null, 5)] [InlineData(null, true, 3)] [InlineData(null, false, 5)]
    [InlineData("", null, 5)] [InlineData("  ", null, 5)]
    [InlineData("PICKUP", null, 1)] [InlineData("PICKUP", true, 0)]
    [InlineData("PAYMENT", true, 1)] [InlineData("SYSTEM", null, 1)]
    [InlineData("LIQUIDATION", null, 2)] [InlineData("LIQUIDATION", true, 1)] [InlineData("LIQUIDATION", false, 2)]
    [InlineData("UNKNOWN", null, 0)] [InlineData("liquidation", null, 0)]
    public async Task List_combines_category_and_unread_filters_before_counting(string? category, bool? unread, int expected)
    {
        await Seed();
        var data = Ok<PagedResult<SellerNotificationDto>>(await Controller().List(category: category, unreadOnly: unread)).Data!;
        Assert.Equal(expected, data.TotalCount); Assert.Equal(expected, data.Items.Count);
        if (unread == true) Assert.All(data.Items, n => Assert.False(n.IsRead));
        if (!string.IsNullOrWhiteSpace(category)) Assert.All(data.Items, n => Assert.Equal(category, n.Category));
    }

    [Theory]
    [InlineData(1, 2, 1, 2, 2)] [InlineData(2, 2, 2, 2, 2)] [InlineData(3, 2, 3, 2, 1)] [InlineData(4, 2, 4, 2, 0)]
    [InlineData(0, 2, 1, 2, 2)] [InlineData(-5, 2, 1, 2, 2)]
    [InlineData(1, 0, 1, 20, 5)] [InlineData(1, -1, 1, 20, 5)] [InlineData(1, 101, 1, 20, 5)]
    [InlineData(1, 100, 1, 100, 5)] [InlineData(1, int.MaxValue, 1, 20, 5)]
    [InlineData(int.MaxValue, 100, int.MaxValue, 100, 0)]
    public async Task List_bounds_pagination_and_keeps_filtered_total(int page, int size, int expectedPage, int expectedSize, int count)
    {
        await Seed();
        var data = Ok<PagedResult<SellerNotificationDto>>(await Controller().List(page, size)).Data!;
        Assert.Equal(expectedPage, data.Page); Assert.Equal(expectedSize, data.PageSize);
        Assert.Equal(5, data.TotalCount); Assert.Equal(count, data.Items.Count);
    }

    [Fact]
    public async Task List_pages_have_no_gaps_or_duplicates_when_timestamps_match()
    {
        await Seed();
        var first = Ok<PagedResult<SellerNotificationDto>>(await Controller().List(1, 1, category: "LIQUIDATION")).Data!;
        var second = Ok<PagedResult<SellerNotificationDto>>(await Controller().List(2, 1, category: "LIQUIDATION")).Data!;
        Assert.Equal(2, first.TotalCount); Assert.Equal(2, second.TotalCount);
        Assert.Equal(Id(4), Assert.Single(first.Items).Id); Assert.Equal(Id(5), Assert.Single(second.Items).Id);
    }

    [Theory]
    [InlineData("list")] [InlineData("count")] [InlineData("all")]
    public async Task Empty_account_returns_empty_list_or_zero_even_when_other_users_have_notices(string action)
    {
        db.Notifications.Add(new Notification { UserId = other.Id, Title = "Chỉ Seller khác", CreatedAt = now });
        await db.SaveChangesAsync();
        var result = await Invoke(Controller(), action);
        if (action == "list")
        {
            var data = Ok<PagedResult<SellerNotificationDto>>(result).Data!;
            Assert.Empty(data.Items); Assert.Equal(0, data.TotalCount);
        }
        else Assert.Equal(0, Ok<int>(result).Data);
        await using var verify = Open();
        Assert.False((await verify.Notifications.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task Unread_count_ignores_other_users_and_counts_all_categories()
    {
        await Seed();
        Assert.Equal(3, Ok<int>(await Controller().UnreadCount()).Data);
        await Controller().MarkRead(Id(4));
        Assert.Equal(2, Ok<int>(await Controller().UnreadCount()).Data);
        await Controller().MarkUnread(Id(4));
        Assert.Equal(3, Ok<int>(await Controller().UnreadCount()).Data);
    }

    [Theory]
    [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(false, false)]
    public async Task Mark_read_or_unread_is_idempotent_persists_and_preserves_other_fields(bool markRead, bool initiallyRead)
    {
        await Seed();
        var before = await db.Notifications.SingleAsync(n => n.Id == Id(4));
        before.IsRead = initiallyRead; await db.SaveChangesAsync();
        var controller = Controller();
        for (var i = 0; i < 2; i++)
        {
            var result = markRead ? await controller.MarkRead(Id(4)) : await controller.MarkUnread(Id(4));
            Assert.Equal(markRead ? "Đã đánh dấu đã đọc." : "Đã đánh dấu chưa đọc.", Ok<string>(result).Data);
        }
        await using var verify = Open();
        var after = await verify.Notifications.SingleAsync(n => n.Id == Id(4));
        Assert.Equal(markRead, after.IsRead); Assert.Equal(before.UserId, after.UserId);
        Assert.Equal(before.Title, after.Title); Assert.Equal(before.Message, after.Message);
        Assert.Equal(before.Category, after.Category); Assert.Equal(before.TargetType, after.TargetType);
        Assert.Equal(before.TargetId, after.TargetId); Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(7, await verify.Notifications.CountAsync());
        Assert.False((await verify.Notifications.SingleAsync(n => n.Id == Id(6))).IsRead);
        Assert.True((await verify.Notifications.SingleAsync(n => n.Id == Id(7))).IsRead);
    }

    [Theory]
    [InlineData(true, "foreign-unread")] [InlineData(true, "foreign-read")] [InlineData(true, "missing")] [InlineData(true, "empty")]
    [InlineData(false, "foreign-unread")] [InlineData(false, "foreign-read")] [InlineData(false, "missing")] [InlineData(false, "empty")]
    public async Task Mark_rejects_foreign_missing_and_empty_ids_with_identical_404_without_writes(bool markRead, string kind)
    {
        await Seed();
        var id = kind switch { "foreign-unread" => Id(6), "foreign-read" => Id(7), "empty" => Guid.Empty, _ => Id(999) };
        var result = markRead ? await Controller().MarkRead(id) : await Controller().MarkUnread(id);
        var response = Assert.IsType<ApiResponse<string>>(Assert.IsType<NotFoundObjectResult>(result).Value);
        Assert.False(response.Success); Assert.Equal("Không tìm thấy thông báo.", response.Message);
        await using var verify = Open();
        Assert.Equal(new[] { Id(2), Id(5), Id(7) }, await verify.Notifications.Where(n => n.IsRead).OrderBy(n => n.Id).Select(n => n.Id).ToArrayAsync());
        Assert.Equal(7, await verify.Notifications.CountAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Read_all_updates_only_own_unread_items_and_returns_affected_count_then_zero(bool alreadyRead)
    {
        await Seed();
        if (alreadyRead) await db.Notifications.Where(n => n.UserId == seller.Id).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        Assert.Equal(alreadyRead ? 0 : 3, Ok<int>(await Controller().ReadAll()).Data);
        Assert.Equal(0, Ok<int>(await Controller().ReadAll()).Data);
        await using var verify = Open();
        Assert.Equal(5, await verify.Notifications.CountAsync(n => n.UserId == seller.Id && n.IsRead));
        Assert.False((await verify.Notifications.SingleAsync(n => n.Id == Id(6))).IsRead);
        Assert.Equal(7, await verify.Notifications.CountAsync());
    }

    [Theory]
    [InlineData("list")] [InlineData("count")] [InlineData("read")] [InlineData("unread")] [InlineData("all")]
    public async Task Missing_or_malformed_identity_never_reads_or_updates_data(string action)
    {
        await Seed();
        foreach (var identity in new[] { "", "not-a-guid" })
        {
            var controller = Controller(identity);
            if (identity == "") controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            var error = await Record.ExceptionAsync(() => Invoke(controller, action));
            Assert.NotNull(error); Assert.IsType<UnauthorizedAccessException>(error.GetBaseException());
        }
        await using var verify = Open();
        Assert.Equal(3, await verify.Notifications.CountAsync(n => n.IsRead));
    }

    [Theory]
    [InlineData("list")] [InlineData("count")] [InlineData("read")] [InlineData("unread")] [InlineData("all")]
    public async Task Cancelled_request_propagates_cancellation_without_changes(string action)
    {
        await Seed();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Invoke(Controller(), action, cancellation.Token));
        await using var verify = Open();
        Assert.Equal(3, await verify.Notifications.CountAsync(n => n.IsRead));
    }

    [Fact]
    public void All_five_routes_require_seller_authorization_in_controller_metadata()
    {
        var type = typeof(SellerNotificationController);
        Assert.Equal("SELLER", Assert.Single(type.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Equal("api/seller/notifications", Assert.Single(type.GetCustomAttributes<RouteAttribute>()).Template);
        foreach (var name in new[] { "List", "UnreadCount", "MarkRead", "MarkUnread", "ReadAll" })
            Assert.Empty(type.GetMethod(name)!.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Null(type.GetMethod("List")!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("unread-count", type.GetMethod("UnreadCount")!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{id:guid}/read", type.GetMethod("MarkRead")!.GetCustomAttribute<HttpPatchAttribute>()!.Template);
        Assert.Equal("{id:guid}/unread", type.GetMethod("MarkUnread")!.GetCustomAttribute<HttpPatchAttribute>()!.Template);
        Assert.Equal("read-all", type.GetMethod("ReadAll")!.GetCustomAttribute<HttpPatchAttribute>()!.Template);
    }

    [Fact]
    public async Task Send_pickup_persists_metadata_and_can_be_filtered_and_opened_by_seller_api()
    {
        var pickup = new PickupRequest { SellerId = seller.Id, Address = "Địa chỉ kiểm thử" };
        db.PickupRequests.Add(pickup); await db.SaveChangesAsync();
        await new NotificationService(db).SendPickupAsync(seller.Id, pickup.Id, "Đơn đã được nhận", "Nhân viên sắp đến.");
        await using var verify = Open();
        var stored = await verify.Notifications.SingleAsync();
        Assert.Equal(seller.Id, stored.UserId); Assert.False(stored.IsRead);
        var item = Assert.Single(Ok<PagedResult<SellerNotificationDto>>(await Controller().List(category: "PICKUP", unreadOnly: true)).Data!.Items);
        Assert.Equal("Đơn đã được nhận", item.Title); Assert.Equal("Nhân viên sắp đến.", item.Message);
        Assert.Equal(pickup.Id, item.PickupRequestId); Assert.Equal(pickup.Id, item.TargetId); Assert.Equal("PICKUP_REQUEST", item.TargetType);
    }
}
