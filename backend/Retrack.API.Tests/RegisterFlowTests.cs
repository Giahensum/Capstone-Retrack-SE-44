using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Xunit;

namespace Retrack.API.Tests;

public class RegisterFlowTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration FakeConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "SuperSecretKeyForRetrackTesting12345678901234567890",
            ["Jwt:Issuer"] = "RetrackTest",
            ["Jwt:Audience"] = "RetrackUsers"
        }).Build();

    private static AuthService CreateAuthService(AppDbContext db)
    {
        var userRepo = new UserRepository(db);
        var config = FakeConfig();
        return new AuthService(userRepo, config, null!, null!);
    }

    [Theory]
    [InlineData("SELLER")]
    [InlineData("DEPOT_OWNER")]
    [InlineData("FACTORY")]
    public async Task Register_AllowedRoles_SucceedsWithJwtAndActiveUser(string role)
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var dto = new RegisterDto
        {
            Email = $"user_{role.ToLower()}@retrack.vn",
            Password = "Password123",
            FullName = $"Test User {role}",
            Phone = "0901234567",
            Role = role
        };

        var response = await service.RegisterAsync(dto);

        Assert.NotNull(response);
        Assert.Equal(role, response.Role);
        Assert.Equal(dto.Email.ToLowerInvariant(), response.Email.ToLowerInvariant());
        Assert.Equal(dto.FullName, response.FullName);
        Assert.NotEmpty(response.Token);

        // Verify in DB
        var saved = await db.Users.FindAsync(response.UserId);
        Assert.NotNull(saved);
        Assert.True(saved.IsActive);
        Assert.Equal(role, saved.Role);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123", saved.PasswordHash));
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("DRIVER")]
    [InlineData("DEPOT_EMPLOYEE")]
    [InlineData("MODERATOR")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Register_ProhibitedOrInvalidRoles_ThrowsArgumentException(string? role)
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var dto = new RegisterDto
        {
            Email = "prohibited@retrack.vn",
            Password = "Password123",
            FullName = "Hacker User",
            Phone = "0901234567",
            Role = role!
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(dto));
        Assert.Contains("không được phép tự đăng ký", ex.Message);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var dto1 = new RegisterDto
        {
            Email = "duplicate@retrack.vn",
            Password = "Password123",
            FullName = "First Registrant",
            Phone = "0901111111",
            Role = "SELLER"
        };
        await service.RegisterAsync(dto1);

        // Try registering again with same email
        var dto2 = new RegisterDto
        {
            Email = "duplicate@retrack.vn",
            Password = "Password456",
            FullName = "Second Registrant",
            Phone = "0902222222",
            Role = "SELLER"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(dto2));
        Assert.Contains("Email đã được sử dụng", ex.Message);
    }

    [Fact]
    public async Task Register_DuplicateEmail_DifferentCase_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var dto1 = new RegisterDto
        {
            Email = "case_test@retrack.vn",
            Password = "Password123",
            FullName = "First Registrant",
            Phone = "0901111111",
            Role = "SELLER"
        };
        await service.RegisterAsync(dto1);

        // Try registering again with UPPERCASE email
        var dto2 = new RegisterDto
        {
            Email = "CASE_TEST@RETRACK.VN",
            Password = "Password456",
            FullName = "Second Registrant",
            Phone = "0902222222",
            Role = "SELLER"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(dto2));
        Assert.Contains("Email đã được sử dụng", ex.Message);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@nodomain.com")]
    [InlineData("user@")]
    [InlineData("")]
    public async Task Register_InvalidEmail_ThrowsArgumentException(string email)
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var dto = new RegisterDto
        {
            Email = email,
            Password = "Password123",
            FullName = "Invalid Email User",
            Phone = "0901234567",
            Role = "SELLER"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(dto));
    }

    [Theory]
    [InlineData("12345")] // 5 chars (min is 6)
    [InlineData("")]
    public async Task Register_ShortPassword_ThrowsArgumentException(string pwd)
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var dto = new RegisterDto
        {
            Email = "shortpwd@retrack.vn",
            Password = pwd,
            FullName = "Short Pwd User",
            Phone = "0901234567",
            Role = "SELLER"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task Register_EmptyFullName_ThrowsArgumentException()
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var dto = new RegisterDto
        {
            Email = "nofullname@retrack.vn",
            Password = "Password123",
            FullName = "   ",
            Phone = "0901234567",
            Role = "SELLER"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task Register_ThenLogin_AuthenticatesSuccessfully()
    {
        await using var db = NewDb();
        var service = CreateAuthService(db);

        var regDto = new RegisterDto
        {
            Email = "login_after_reg@retrack.vn",
            Password = "SecurePassword2026",
            FullName = "Login After Reg User",
            Phone = "0909999999",
            Role = "SELLER"
        };

        var regRes = await service.RegisterAsync(regDto);
        Assert.NotNull(regRes);

        // Immediate Login
        var loginDto = new LoginDto
        {
            Email = "login_after_reg@retrack.vn",
            Password = "SecurePassword2026"
        };

        var loginRes = await service.LoginAsync(loginDto);
        Assert.NotNull(loginRes);
        Assert.Equal(regRes.UserId, loginRes.UserId);
        Assert.Equal("SELLER", loginRes.Role);
        Assert.Equal("login_after_reg@retrack.vn", loginRes.Email);
    }
}
