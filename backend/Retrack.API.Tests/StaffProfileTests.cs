using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Staff;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Staff;
using Xunit;

namespace Retrack.API.Tests;

public class StaffProfileTests
{
    private sealed class Images : ICloudinaryService
    {
        public Task<string> UploadAvatarAsync(Stream stream, string name) => UploadImageAsync(stream, name);
        public Task<string> UploadImageAsync(Stream stream, string name) => Task.FromResult("https://example.com/avatar.jpg");
        public Task<bool> DeleteImageAsync(string id) => Task.FromResult(true);
    }
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<DepotStaff> Seed(AppDbContext db, string role = "DEPOT_EMPLOYEE")
    {
        var staff = new DepotStaff
        {
            User = new User { Role = role, FullName = "Lê Minh Tuấn", Email = "employee@retrack.vn", Phone = "0933333333" },
            Depot = new Depot { Owner = new User { Role = "DEPOT_OWNER" }, Name = "Kho A", Address = "TP.HCM" },
            StaffType = role
        };
        db.DepotStaffs.Add(staff);
        await db.SaveChangesAsync();
        return staff;
    }
    private static StaffProfileService Service(AppDbContext db) => new(new StaffProfileRepository(db), new Images());

    [Theory]
    [InlineData("DEPOT_EMPLOYEE")]
    [InlineData("DRIVER")]
    public async Task ProfileReturnsOwnDepotAndPersistsOnlyOwnPhone(string role)
    {
        await using var db = NewDb();
        var staff = await Seed(db, role);
        var other = await Seed(db, role);
        var response = await Service(db).UpdateAsync(staff.UserId, role, new() { Phone = "0987654321" }, default);
        Assert.Equal(staff.DepotId, response.DepotId);
        Assert.Equal("0987654321", response.Phone);
        Assert.Equal("0933333333", other.User.Phone);
        Assert.Equal("Lê Minh Tuấn", response.FullName);
        Assert.Equal(role, response.Role);
    }
    [Fact]
    public async Task WrongRoleUnknownUserAndInactiveMembershipAreRejected()
    {
        await using var db = NewDb();
        var staff = await Seed(db);
        var service = Service(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(staff.UserId, "DRIVER", default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(Guid.NewGuid(), "DEPOT_EMPLOYEE", default));
        staff.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(staff.UserId, "DEPOT_EMPLOYEE", new() { Phone = "0987654321" }, default));
        Assert.Equal("0933333333", staff.User.Phone);
        staff.IsActive = true;
        staff.User.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(staff.UserId, "DEPOT_EMPLOYEE", default));
    }
    [Fact]
    public async Task AvatarRejectsDisguisedFileAndPreservesPreviousImage()
    {
        await using var db = NewDb();
        var staff = await Seed(db);
        staff.User.AvatarUrl = "https://example.com/old.jpg";
        using var stream = new MemoryStream("not an image"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "file", "fake.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UploadAvatarAsync(staff.UserId, "DEPOT_EMPLOYEE", file, default));
        Assert.Equal("https://example.com/old.jpg", staff.User.AvatarUrl);
    }
    [Theory]
    [InlineData("0933333333", true)]
    [InlineData("+84933333333", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void PhoneValidation(string phone, bool valid)
    {
        var request = new UpdateStaffProfileRequest { Phone = phone };
        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true));
    }
    [Theory]
    [InlineData("ADMIN")]
    [InlineData("DEPOT_EMPLOYEE")]
    [InlineData("DRIVER")]
    public async Task PublicRegistrationCannotCreatePrivilegedAccounts(string role)
    {
        await using var db = NewDb();
        var auth = new AuthService(new UserRepository(db), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() => auth.RegisterAsync(new RegisterDto { Role = role }));
        Assert.Empty(db.Users);
    }

    [Theory]
    [InlineData("DRIVER")]
    [InlineData("DEPOT_EMPLOYEE")]
    public async Task LockedDepotOwnerCannotSupplyActiveStaffProfile(string role)
    {
        await using var db = NewDb();
        var staff = await Seed(db, role);
        staff.Depot.Owner.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db).GetAsync(staff.UserId, role, default));
    }

    [Fact]
    public async Task DriverProfileUsesDriverCodeAndRejectsEmployeeRoleAndInvalidPhone()
    {
        await using var db = NewDb();
        var staff = await Seed(db, "DRIVER");
        var service = Service(db);
        var profile = await service.GetAsync(staff.UserId, "DRIVER", default);
        Assert.StartsWith("DRV-", profile.EmployeeCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(staff.UserId, "DEPOT_EMPLOYEE", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(staff.UserId, "DRIVER", new() { Phone = "123" }, default));
        Assert.Equal("0933333333", staff.User.Phone);
    }

    [Fact]
    public async Task DriverAvatarPersistsWithoutChangingOtherStaff()
    {
        await using var db = NewDb();
        var driver = await Seed(db, "DRIVER");
        var employee = await Seed(db);
        using var stream = new MemoryStream(new byte[] { 255, 216, 255, 0, 0, 0, 0, 0 });
        var file = new FormFile(stream, 0, stream.Length, "file", "avatar.jpg");
        var profile = await Service(db).UploadAvatarAsync(driver.UserId, "DRIVER", file, default);
        Assert.Equal("https://example.com/avatar.jpg", profile.AvatarUrl);
        db.ChangeTracker.Clear();
        Assert.Equal(profile.AvatarUrl, (await db.Users.FindAsync(driver.UserId))!.AvatarUrl);
        Assert.Null((await db.Users.FindAsync(employee.UserId))!.AvatarUrl);
    }
}
