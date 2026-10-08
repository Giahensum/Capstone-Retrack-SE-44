using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Driver;
using Xunit;

namespace Retrack.API.Tests;

public sealed class DriverDeliveryTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly EmployeeCollectionTests.TestImages images = new();
    private static User UserFor(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.local" };
    private readonly User driver = UserFor("DRIVER"), other = UserFor("DRIVER");
    private TransportJob job = null!;
    private DriverDeliveryService Service => new(db, new(db), images);
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var depot = new Depot { Owner = UserFor("DEPOT_OWNER"), Latitude = 16.05m, Longitude = 108.2m };
        db.DepotStaffs.AddRange(new DepotStaff { User = driver, Depot = depot, StaffType = "DRIVER" },
            new DepotStaff { User = other, Depot = depot, StaffType = "DRIVER" });
        job = new() { Driver = driver, Status = "ACCEPTED", Batch = new() { Depot = depot, Status = "TRANSPORT_READY",
            MaterialType = "PET", DeclaredWeightKg = 100,
            TargetFactory = new() { Owner = UserFor("FACTORY"), Latitude = 16.08m, Longitude = 108.22m } } };
        db.TransportJobs.Add(job); await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private static EmployeeCheckInRequest Evidence(bool factory = false) => new() {
        Latitude = factory ? 16.08 : 16.05, Longitude = factory ? 108.22 : 108.2, AccuracyMeters = 8,
        PhotoTakenAt = DateTimeOffset.UtcNow, LocationRecordedAt = DateTimeOffset.UtcNow,
        ImageFile = new FormFile(new MemoryStream(new byte[] {255,216,255,224,0,0,0,0}), 0, 8, "imageFile", "test.jpg") };
    private Task Act(string action, EmployeeCheckInRequest? input = null, string? reason = null, Guid? operation = null, Guid? user = null) =>
        Service.ExecuteAsync(user ?? driver.Id, job.Id, action, new(operation ?? Guid.NewGuid(), reason), input, default);

    [Fact]
    public async Task FullLifecyclePersistsEvidenceAndRetriesWithoutExtraUploads()
    {
        var operation = Guid.NewGuid();
        await Act("checkin", Evidence(), operation: operation);
        await Act("checkin", Evidence(), operation: operation);
        Assert.Equal(1, images.UploadCount);
        Assert.Equal("PICKED_UP", job.Status); Assert.Equal("IN_TRANSIT", job.Batch.Status);
        await Act("start");
        await Act("checkout", Evidence(true));
        db.ChangeTracker.Clear();
        var saved = await db.TransportJobs.Include(j => j.Batch).SingleAsync();
        Assert.Equal("DELIVERED", saved.Status); Assert.Equal("DELIVERED", saved.Batch.Status);
        Assert.Null(saved.Batch.FactoryReceivedAt); Assert.Null(saved.Batch.SettledAt);
        Assert.Equal(3, await db.DriverDeliveryEvents.CountAsync());
        Assert.Equal(5, await db.Notifications.CountAsync());
        Assert.Equal(2, images.UploadCount);
        Assert.Empty(await db.PickupRequests.ToListAsync());
        Assert.Empty(await db.PlatformTransactions.ToListAsync());
        Assert.Equal(3, (await Service.GetAsync(driver.Id, job.Id, default)).Events.Count);
    }
    [Fact]
    public async Task CancellationRequeuesAndOldRetryDoesNotCancelNewAssignment()
    {
        var operation = Guid.NewGuid();
        await Act("cancel", reason: "Xe hỏng chưa thể nhận hàng", operation: operation);
        Assert.Null(job.DriverId); Assert.Equal("PENDING", job.Status);
        Assert.Equal("TRANSPORT_READY", job.Batch.Status);
        await new DriverJobService(db).AcceptAsync(other.Id, job.Id, default);
        await Act("cancel", reason: "Xe hỏng chưa thể nhận hàng", operation: operation);
        await db.Entry(job).ReloadAsync();
        Assert.Equal(other.Id, job.DriverId); Assert.Equal("ACCEPTED", job.Status);
        Assert.Single(await db.DriverDeliveryEvents.ToListAsync());
    }
    [Fact]
    public async Task RejectOnlyHidesPersonalPoolAndLeavesOtherDriversEligible()
    {
        job.DriverId = null; job.Status = "PENDING"; await db.SaveChangesAsync();
        await Act("reject", reason: "Không phù hợp lịch làm việc");
        var jobs = new DriverJobService(db);
        Assert.Empty((await jobs.ListAsync(driver.Id, false, 1, default)).Items);
        Assert.Single((await jobs.ListAsync(other.Id, false, 1, default)).Items);
        Assert.Equal("PENDING", job.Status); Assert.Null(job.DriverId);
    }
    [Fact]
    public async Task IncidentAfterPickupNeverReleasesCargoOrAssignment()
    {
        await Act("checkin", Evidence());
        await Assert.ThrowsAsync<DepotConflictException>(() => Act("cancel", reason: "Xe bị hỏng trên đường đi"));
        await Act("incident", reason: "Xe bị hỏng trên đường đi");
        Assert.Equal("PICKED_UP", job.Status); Assert.Equal(driver.Id, job.DriverId);
        Assert.Equal("IN_TRANSIT", job.Batch.Status);
        Assert.Equal(2, await db.DriverDeliveryEvents.CountAsync());
    }
    [Theory]
    [InlineData("start")]
    [InlineData("checkout")]
    [InlineData("incident")]
    public async Task OutOfOrderActionsHaveNoEffects(string action)
    {
        await Assert.ThrowsAsync<DepotConflictException>(() => Act(action, Evidence(true), "Lý do kiểm thử hợp lệ"));
        Assert.Empty(await db.DriverDeliveryEvents.ToListAsync()); Assert.Equal(0, images.UploadCount);
    }
    [Theory]
    [InlineData("far")]
    [InlineData("accuracy")]
    [InlineData("old")]
    [InlineData("mock")]
    [InlineData("photo")]
    [InlineData("format")]
    [InlineData("missing")]
    public async Task BadEvidenceRejectedBeforeUpload(string kind)
    {
        var input = Evidence();
        switch (kind) {
            case "far": input.Latitude = 10; break;
            case "accuracy": input.AccuracyMeters = 51; break;
            case "old": input.LocationRecordedAt = DateTimeOffset.UtcNow.AddMinutes(-3); break;
            case "mock": input.IsMocked = true; break;
            case "photo": input.PhotoTakenAt = DateTimeOffset.UtcNow.AddMinutes(-11); break;
            case "format": input.ImageFile = new FormFile(new MemoryStream(new byte[8]), 0, 8, "imageFile", "fake.jpg"); break;
            case "missing": input.Latitude = null; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Act("checkin", input));
        Assert.Equal(0, images.UploadCount); Assert.Empty(await db.DriverDeliveryEvents.ToListAsync());
    }
    [Fact]
    public async Task OtherDriverAndInactiveMembershipCannotMutate()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Act("checkin", Evidence(), user: other.Id));
        driver.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Act("checkin", Evidence()));
        Assert.Equal(0, images.UploadCount);
    }
    [Fact]
    public async Task UploadFailureRollsBackStateAndNotices()
    {
        images.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Act("checkin", Evidence()));
        db.ChangeTracker.Clear();
        Assert.Equal("ACCEPTED", (await db.TransportJobs.SingleAsync()).Status);
        Assert.Equal("TRANSPORT_READY", (await db.InventoryBatches.SingleAsync()).Status);
        Assert.Empty(await db.Notifications.ToListAsync()); Assert.Empty(await db.DriverDeliveryEvents.ToListAsync());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("ngắn")]
    public async Task CancellationRequiresReason(string? reason) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Act("cancel", reason: reason));
    [Fact]
    public async Task CheckoutValidatesFactoryRatherThanDepot()
    {
        await Act("checkin", Evidence()); await Act("start");
        await Assert.ThrowsAsync<ArgumentException>(() => Act("checkout", Evidence()));
        Assert.Equal("IN_TRANSIT", job.Status); Assert.Equal(1, images.UploadCount);
    }
    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("RECEIVED")]
    [InlineData("VERIFIED")]
    [InlineData("COMPLETED")]
    public async Task TerminalBatchCannotBeOverwritten(string state)
    {
        await Act("checkin", Evidence()); await Act("start");
        job.Batch.Status = state; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(() => Act("checkout", Evidence(true)));
        Assert.Equal(state, job.Batch.Status); Assert.Equal(1, images.UploadCount);
    }
    [Fact]
    public async Task WrongDepotCannotReadOrWriteEvidence()
    {
        var stranger = UserFor("DRIVER");
        db.DepotStaffs.Add(new() { User = stranger, Depot = new() { Owner = UserFor("DEPOT_OWNER") }, StaffType = "DRIVER" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Act("checkin", Evidence(), user: stranger.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.GetAsync(stranger.Id, job.Id, default));
        Assert.Equal(0, images.UploadCount);
    }
    [Fact]
    public async Task DuplicateOperationCannotChangeActionOrReason()
    {
        var operation = Guid.NewGuid();
        await Act("checkin", Evidence(), operation: operation);
        await Assert.ThrowsAsync<DepotConflictException>(() => Act("start", operation: operation));
        Assert.Equal("PICKED_UP", job.Status);
    }
    [Fact]
    public async Task OversizedReasonAndMissingOperationHaveNoEffects()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Act("cancel", reason: new string('a', 1001)));
        await Assert.ThrowsAsync<ArgumentException>(() => Act("checkin", Evidence(), operation: Guid.Empty));
        Assert.Empty(await db.DriverDeliveryEvents.ToListAsync());
    }
}
