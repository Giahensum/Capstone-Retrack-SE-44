using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Xunit;

namespace Retrack.API.Tests
{
    /// <summary>
    /// Khóa hành vi đăng ký qua service: User và hồ sơ Factory được lưu cùng một lần,
    /// không còn controller tự ghi DbContext. Chạy trên InMemory nên không cần PostgreSQL.
    /// </summary>
    public class AuthRegistrationTests
    {
        private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static AuthService Service(AppDbContext db) => new(
            new UserRepository(db),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "test-only-signing-key-0123456789-abcdefghijklmnop",
                    ["Jwt:Issuer"] = "retrack-tests",
                    ["Jwt:Audience"] = "retrack-tests"
                })
                .Build(),
            null!,
            null!,
            new FactoryRepository(db));

        private static RegisterDto Valid(string role, string email, string phone) => new()
        {
            Email = email,
            FullName = "Nguyen Van Test",
            Phone = phone,
            Password = "TestOnly123!",
            Role = role
        };

        [Fact]
        public async Task Factory_registration_creates_user_and_factory_together()
        {
            await using var db = NewDb();

            var result = await Service(db).RegisterAsync(Valid("FACTORY", "factory@test.invalid", "0912345678"));

            Assert.False(string.IsNullOrWhiteSpace(result.Token));
            var user = await db.Users.SingleAsync(u => u.Email == "factory@test.invalid");
            var factory = await db.Factories.SingleAsync(f => f.OwnerId == user.Id);
            Assert.Equal("Nguyen Van Test", factory.Name);
        }

        [Fact]
        public async Task Seller_registration_does_not_create_factory()
        {
            await using var db = NewDb();

            await Service(db).RegisterAsync(Valid("SELLER", "seller@test.invalid", "0987654321"));

            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(0, await db.Factories.CountAsync());
        }

        [Fact]
        public async Task Duplicate_email_is_rejected_without_creating_extra_rows()
        {
            await using var db = NewDb();
            var service = Service(db);

            await service.RegisterAsync(Valid("SELLER", "dup@test.invalid", "0912345678"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterAsync(Valid("SELLER", "dup@test.invalid", "0987654321")));
            Assert.Equal(1, await db.Users.CountAsync());
        }
    }
}
