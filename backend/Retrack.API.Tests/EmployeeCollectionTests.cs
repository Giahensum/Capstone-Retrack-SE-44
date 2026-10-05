using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Interfaces;
using Xunit;

namespace Retrack.API.Tests;

public sealed partial class EmployeeCollectionTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly TestImages images = new();
    private readonly User employee = UserFor("DEPOT_EMPLOYEE"), seller = UserFor("SELLER"), colleague = UserFor("DEPOT_EMPLOYEE");
    private readonly Depot depot = new() { Owner = UserFor("DEPOT_OWNER"), Name = "Kho kiểm thử" };
    private PickupRequest pickup = null!;
    private EmployeeCollectionService Service => new(db, images);
    private static User UserFor(string role) => new() { Role = role, Email = $"{Guid.NewGuid():N}@test.local", FullName = role };
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        connection.CreateFunction("NOW", () => DateTime.UtcNow.ToString("O"));
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.DepotStaffs.AddRange(new DepotStaff { User = employee, Depot = depot, StaffType = employee.Role },
            new DepotStaff { User = colleague, Depot = depot, StaffType = colleague.Role });
        pickup = new() { Seller = seller, TargetDepot = depot, AcceptedCollector = employee, Status = "SCHEDULED",
            Address = "Điểm test", Latitude = 16.05m, Longitude = 108.2m, PlatformFeePercentage = 3m };
        db.PickupRequests.Add(pickup);
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private static EmployeeCheckInRequest Request() => new()
    {
        Latitude = 16.05, Longitude = 108.2, AccuracyMeters = 10,
        LocationRecordedAt = DateTimeOffset.UtcNow, PhotoTakenAt = DateTimeOffset.UtcNow,
        ImageFile = new FormFile(new MemoryStream(new byte[] { 255, 216, 255, 224, 0, 0, 0, 0 }), 0, 8, "imageFile", "test.jpg")
    };
    private async Task<EmployeeCollectionDto> CheckIn() => await Service.CheckInAsync(employee.Id, pickup.Id, Request(), default);
    private Task<EmployeeCollectionDto> Save(int revision, params ClassificationItemInput[] items) =>
        Service.SaveAsync(employee.Id, pickup.Id, new(revision, items.ToList()), default);

    [Fact]
    public async Task CheckInPersistsProofTransitionsAndRetryDoesNotDuplicate()
    {
        var result = await CheckIn();
        Assert.Equal("IN_PROGRESS", result.Status); Assert.True(result.CanEdit);
        Assert.NotNull(result.CheckIn); Assert.Equal(0, result.CheckIn.DistanceMeters, 3);
        Assert.Equal(employee.Id, (await db.PickupCheckIns.SingleAsync()).EmployeeId);
        Assert.Equal(result.CheckIn.ImageUrl, (await db.PickupRequests.AsNoTracking().SingleAsync()).CheckinImageUrl);
        await CheckIn();
        Assert.Equal(1, images.UploadCount); Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task OwnershipAndMembershipAreRequiredBeforeUploading()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.CheckInAsync(colleague.Id, pickup.Id, Request(), default));
        var staff = await db.DepotStaffs.SingleAsync(s => s.UserId == employee.Id);
        staff.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => CheckIn());
        Assert.Equal(0, images.UploadCount);
    }

    [Fact]
    public async Task OtherDepotAndInactiveOwnerCannotAccessOrMutate()
    {
        var other = new Depot { Owner = UserFor("DEPOT_OWNER"), Name = "Kho khác" };
        db.Depots.Add(other); pickup.TargetDepot = other; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.GetAsync(employee.Id, pickup.Id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => CheckIn());
        pickup.TargetDepot = depot; depot.Owner.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => CheckIn());
        Assert.Equal(0, images.UploadCount);
    }

    [Theory]
    [InlineData("PENDING")][InlineData("CANCELLED")][InlineData("WEIGHED")][InlineData("DONE")]
    public async Task InvalidStateCannotCheckIn(string status)
    {
        pickup.Status = status; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(() => CheckIn());
        Assert.Equal(0, images.UploadCount);
    }

    [Fact]
    public async Task UploadFailureRollsBackStateAndNotification()
    {
        images.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CheckIn());
        Assert.Equal("SCHEDULED", (await db.PickupRequests.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await db.PickupCheckIns.ToListAsync()); Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task InvalidImageCannotBeDisguisedByItsExtension()
    {
        var request = Request();
        request.ImageFile = new FormFile(new MemoryStream("not jpeg"u8.ToArray()), 0, 8, "imageFile", "photo.jpg");
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CheckInAsync(employee.Id, pickup.Id, request, default));
        Assert.Equal(0, images.UploadCount);
    }

    [Fact]
    public async Task ClassificationPersistsRoundedTotalsSupportsEditDeleteAndDoesNotChangeAccounting()
    {
        await Assert.ThrowsAsync<DepotConflictException>(() => Save(0, new ClassificationItemInput("PET", 2, 5000)));
        await CheckIn();
        var result = await Save(0, new("PET", 1.25m, 1001), new("IRON", 2, 10000));
        Assert.Equal(21251, result.GrossAmount); Assert.Equal(3.25m, result.TotalWeightKg);
        Assert.Equal(1, result.Revision); Assert.Equal("IN_PROGRESS", result.Status);
        db.ChangeTracker.Clear();
        var persisted = await Service.GetAsync(employee.Id, pickup.Id, default);
        Assert.Equal(result.GrossAmount, persisted.GrossAmount);
        var financial = await db.PickupRequests.AsNoTracking().SingleAsync();
        Assert.Equal(0, financial.GrossAmount); Assert.Equal(3m, financial.PlatformFeePercentage);
        Assert.Equal(0, financial.PlatformFeeAmount); Assert.Equal(0, financial.NetAmount);
        var edited = await Save(1, new ClassificationItemInput("PET", 3, 2000)); Assert.Single(edited.Items); Assert.Equal(6000, edited.GrossAmount);
        var empty = await Save(2); Assert.Empty(empty.Items); Assert.Equal(0, empty.GrossAmount);
    }

    [Fact]
    public async Task RetryIsIdempotentButStaleDifferentContentIsRejected()
    {
        await CheckIn();
        await Save(0, new ClassificationItemInput("PET", 1, 5000));
        var retry = await Save(0, new ClassificationItemInput("PET", 1, 5000)); Assert.Equal(1, retry.Revision);
        await Assert.ThrowsAsync<DepotConflictException>(() => Save(0, new ClassificationItemInput("PET", 2, 5000)));
        Assert.Equal(1m, (await db.PickupRequestItems.SingleAsync()).WeightKg);
    }

    [Fact]
    public async Task SubmittedOrderIsReadOnlyAndKeepsDraft()
    {
        await CheckIn(); await Save(0, new ClassificationItemInput("PET", 1, 5000));
        pickup.Status = "WEIGHED"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(() => Save(1));
        Assert.False((await Service.GetAsync(employee.Id, pickup.Id, default)).CanEdit);
        Assert.Single(await db.PickupRequestItems.ToListAsync());
    }

    [Fact]
    public async Task CatalogUsesLatestEffectivePriceAndDoesNotInventMissingPrices()
    {
        var now = DateTime.UtcNow;
        db.MarketPrices.AddRange(new MarketPrice { MaterialType = MaterialType.PET, PricePerKg = 4000, EffectiveDate = now.AddDays(-2) },
            new MarketPrice { MaterialType = MaterialType.PET, PricePerKg = 5000, EffectiveDate = now.AddDays(-1) },
            new MarketPrice { MaterialType = MaterialType.PET, PricePerKg = 9000, EffectiveDate = now.AddDays(1) });
        await db.SaveChangesAsync();
        var catalog = await Service.MaterialsAsync(employee.Id, default);
        Assert.Equal(5000m, catalog.Single(m => m.Code == "PET").ReferencePrice);
        Assert.Null(catalog.Single(m => m.Code == "COPPER").ReferencePrice);
    }

    [Theory]
    [InlineData("PET", "0", "5000")][InlineData("PET", "-1", "5000")]
    [InlineData("PET", "1.001", "5000")][InlineData("PET", "10001", "5000")]
    [InlineData("PET", "1", "0")][InlineData("PET", "1", "-1")]
    [InlineData("PET", "1", "1.5")][InlineData("PET", "1", "10000001")]
    [InlineData("INVALID", "1", "5000")]
    public void InvalidWeighInputIsRejected(string material, string weight, string price)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Throws<ArgumentException>(() => CollectionValidation.Items([new(material, decimal.Parse(weight, culture), decimal.Parse(price, culture))]));
    }

    [Fact]
    public void DuplicateMaterialAndExcessTotalAreRejected()
    {
        Assert.Throws<ArgumentException>(() => CollectionValidation.Items([new("PET", 1, 1), new("Nhựa PET", 2, 1)]));
        Assert.Throws<ArgumentException>(() => CollectionValidation.Items([new("PET", 6000, 1), new("IRON", 6000, 1)]));
    }

    [Theory]
    [InlineData("far")][InlineData("stale")][InlineData("future")][InlineData("accuracy")]
    [InlineData("mock")][InlineData("nan")][InlineData("missing")][InlineData("photo")]
    public void InvalidLocationEvidenceIsRejected(string scenario)
    {
        var r = Request(); var now = DateTimeOffset.UtcNow;
        switch (scenario)
        {
            case "far": r.Latitude = 16.1; break;
            case "stale": r.LocationRecordedAt = now.AddMinutes(-3); break;
            case "future": r.LocationRecordedAt = now.AddMinutes(1); break;
            case "accuracy": r.AccuracyMeters = 51; break;
            case "mock": r.IsMocked = true; break;
            case "nan": r.Latitude = double.NaN; break;
            case "missing": r.Latitude = null; break;
            case "photo": r.PhotoTakenAt = now.AddMinutes(-11); break;
        }
        Assert.Throws<ArgumentException>(() => CollectionValidation.ValidateCheckIn(pickup, r, now));
    }

    internal sealed class TestImages : ICloudinaryService
    {
        public int UploadCount; public bool Fail;
        public Task<string> UploadImageAsync(Stream stream, string name)
        { UploadCount++; if (Fail) throw new InvalidOperationException("Upload thất bại"); return Task.FromResult("https://test.invalid/checkin.jpg"); }
        public Task<string> UploadAvatarAsync(Stream stream, string name) => throw new NotSupportedException();
        public Task<bool> DeleteImageAsync(string id) => Task.FromResult(true);
    }
}
