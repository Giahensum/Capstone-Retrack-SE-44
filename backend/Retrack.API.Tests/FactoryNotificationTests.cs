using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Factory;
using Xunit;

namespace Retrack.API.Tests;

// Cố định hành vi thông báo trước khi chuyển truy vấn sang repository.
public sealed class FactoryNotificationTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly User factory = new() { Role="FACTORY", Email="factory-notice@test.invalid" };
    private readonly User other = new() { Role="FACTORY", Email="other-notice@test.invalid" };
    private static readonly DateTime Now = new(2026,11,28,0,0,0,DateTimeKind.Utc);
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private FactoryNotificationService Service() => new(new Retrack.API.Repositories.FactoryNotificationRepository(db));
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW",()=>Now.ToString("O"));
        db=new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(factory,other);
        db.Notifications.AddRange(
            new Notification { Id=Id(1),User=factory,Title="Thông báo cũ",CreatedAt=Now },
            new Notification { Id=Id(2),User=factory,Title="Thông báo mới",CreatedAt=Now },
            new Notification { Id=Id(3),User=other,Title="Nhà máy khác",CreatedAt=Now });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync();await connection.DisposeAsync(); }
    [Fact] public async Task List_is_scoped_stable_bounded_and_keeps_response_shape()
    {
        var first=await Service().ListAsync(factory.Id,1,1,false,default);
        var second=await Service().ListAsync(factory.Id,2,1,false,default);
        Assert.Equal(2,first.TotalCount);Assert.Equal(2,first.TotalPages);
        Assert.Equal(Id(2),Assert.Single(first.Items).Id);Assert.Equal(Id(1),Assert.Single(second.Items).Id);
        Assert.Empty((await Service().ListAsync(factory.Id,int.MaxValue,100,false,default)).Items);
        var value=System.Text.Json.JsonSerializer.SerializeToElement(first,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(new[]{"items","page","pageSize","totalCount","totalPages"},value.EnumerateObject().Select(p=>p.Name).Order().ToArray());
        Assert.False(value.GetProperty("items")[0].TryGetProperty("user",out _));
    }
    [Fact] public async Task Read_actions_are_scoped_idempotent_and_persist_after_reload()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service().MarkReadAsync(factory.Id,Id(3),true,default));
        await Service().MarkReadAsync(factory.Id,Id(1),true,default);
        await Service().MarkReadAsync(factory.Id,Id(1),true,default);
        Assert.Equal(1,await Service().UnreadCountAsync(factory.Id,default));
        Assert.Single((await Service().ListAsync(factory.Id,1,100,true,default)).Items);
        Assert.Equal(1,await Service().ReadAllAsync(factory.Id,default));
        Assert.Equal(0,await Service().ReadAllAsync(factory.Id,default));
        db.ChangeTracker.Clear();
        Assert.False((await db.Notifications.SingleAsync(n=>n.Id==Id(3))).IsRead);
        await Service().MarkReadAsync(factory.Id,Id(1),false,default);
        db.ChangeTracker.Clear();
        Assert.False((await db.Notifications.SingleAsync(n=>n.Id==Id(1))).IsRead);
    }
    [Fact] public async Task Access_and_paging_rejections_keep_existing_policy()
    {
        await Assert.ThrowsAsync<ArgumentException>(()=>Service().ListAsync(factory.Id,0,20,false,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>Service().ListAsync(factory.Id,1,101,false,default));
        factory.IsActive=false;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service().UnreadCountAsync(factory.Id,default));
        other.Role="SELLER";await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service().ReadAllAsync(other.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service().ListAsync(Guid.NewGuid(),1,20,false,default));
    }
}
