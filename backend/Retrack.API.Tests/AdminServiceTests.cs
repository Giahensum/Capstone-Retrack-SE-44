using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public class AdminServiceTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AdminService NewService(AppDbContext db) => new(
        new UserRepository(db),
        new MarketPriceRepository(db),
        new AuditLogRepository(db),
        new PlatformInvoiceRepository(db),
        new NotificationService(db),
        db);

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 12, 0, 0, DateTimeKind.Utc);

    private static UpsertMarketPriceDto Price(string material, decimal perKg = 10_000m) => new()
    {
        MaterialType = material,
        PricePerKg = perKg,
        EffectiveDate = Utc(2026, 9, 1),
        Source = "Test"
    };

    private static PlatformTransaction Tx(DateTime at, decimal fee, Guid sourceId, string sourceType = "PICKUP_REQUEST")
        => new() { SourceType = sourceType, SourceId = sourceId, FeeAmount = fee, CreatedAt = at };

    // ── Bảng giá thị trường: enum ↔ string ────────────────────────

    [Fact]
    public async Task CreateMarketPriceParsesMaterialNameAndReturnsEnumName()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var created = await service.CreateMarketPriceAsync(Price("pet"), Guid.NewGuid());

        Assert.Equal("PET", created.MaterialType);
        Assert.Equal(MaterialType.PET, (await db.MarketPrices.SingleAsync()).MaterialType);
    }

    [Theory]
    [InlineData("Sắt vụn")]
    [InlineData("Nhựa PET")]
    [InlineData("")]
    [InlineData("99")]
    public async Task CreateMarketPriceRejectsValueOutsideMaterialTypeEnum(string material)
    {
        await using var db = NewDb();
        var service = NewService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateMarketPriceAsync(Price(material), Guid.NewGuid()));
        Assert.Empty(await db.MarketPrices.ToListAsync());
    }

    [Fact]
    public async Task GetMarketPricesFiltersByMaterialType()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var actor = Guid.NewGuid();
        await service.CreateMarketPriceAsync(Price("PET"), actor);
        await service.CreateMarketPriceAsync(Price("IRON", 15_000m), actor);

        var all = await service.GetMarketPricesAsync(null);
        var iron = await service.GetMarketPricesAsync("iron");

        Assert.Equal(2, all.Count);
        Assert.Equal("IRON", Assert.Single(iron).MaterialType);
    }

    [Fact]
    public async Task GetMarketPricesRejectsUnknownMaterialFilter()
    {
        await using var db = NewDb();
        var service = NewService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMarketPricesAsync("Đồng cáp"));
    }

    [Fact]
    public async Task UpdateMarketPriceLogsMaterialAsEnumNameNotOrdinal()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var actor = Guid.NewGuid();
        var created = await service.CreateMarketPriceAsync(Price("PET"), actor);

        var updated = await service.UpdateMarketPriceAsync(created.Id, Price("IRON", 15_000m), actor);

        Assert.Equal("IRON", updated.MaterialType);
        var log = await db.AuditLogs.SingleAsync(a => a.Action == "UPDATE" && a.EntityName == "MarketPrice");
        using var oldData = JsonDocument.Parse(log.OldData!);
        Assert.Equal("PET", oldData.RootElement.GetProperty("MaterialType").GetString());
    }

    [Fact]
    public async Task DeleteMarketPriceLogsMaterialAsEnumName()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var actor = Guid.NewGuid();
        var created = await service.CreateMarketPriceAsync(Price("COPPER"), actor);

        await service.DeleteMarketPriceAsync(created.Id, actor);

        Assert.Empty(await db.MarketPrices.ToListAsync());
        var log = await db.AuditLogs.SingleAsync(a => a.Action == "DELETE" && a.EntityName == "MarketPrice");
        using var oldData = JsonDocument.Parse(log.OldData!);
        Assert.Equal("COPPER", oldData.RootElement.GetProperty("MaterialType").GetString());
    }

    // ── Báo cáo doanh thu: nhãn kỳ ────────────────────────────────

    [Fact]
    public async Task RevenueReportGroupsByIsoWeek()
    {
        await using var db = NewDb();
        db.PlatformTransactions.AddRange(
            Tx(Utc(2026, 1, 1), 100m, Guid.NewGuid()),   // thứ Năm — ISO 2026-W01
            Tx(Utc(2026, 1, 2), 50m, Guid.NewGuid()),    // thứ Sáu — cùng tuần
            Tx(Utc(2026, 1, 8), 25m, Guid.NewGuid()));   // thứ Năm — ISO 2026-W02
        await db.SaveChangesAsync();
        var service = NewService(db);

        var report = await service.GetRevenueReportAsync(Utc(2025, 12, 1), Utc(2026, 2, 1), "week");

        Assert.Equal(175m, report.TotalRevenue);
        Assert.Equal(new[] { "2026-W01", "2026-W02" }, report.Points.Select(p => p.PeriodLabel));
        Assert.Equal(150m, report.Points[0].Amount);
        Assert.Equal(25m, report.Points[1].Amount);
    }

    [Theory]
    [InlineData("day", "2026-01-08")]
    [InlineData("month", "2026-01")]
    [InlineData("WEEK", "2026-W02")]
    [InlineData(null, "2026-01-08")]
    public async Task RevenueReportLabelsPeriodPerGrouping(string? groupBy, string expectedLabel)
    {
        await using var db = NewDb();
        db.PlatformTransactions.Add(Tx(Utc(2026, 1, 8), 40m, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var service = NewService(db);

        var report = await service.GetRevenueReportAsync(Utc(2026, 1, 1), Utc(2026, 1, 31), groupBy!);

        Assert.Equal(expectedLabel, Assert.Single(report.Points).PeriodLabel);
    }

    // ── Người dùng ────────────────────────────────────────────────

    [Fact]
    public async Task SetUserActiveRefusesToDisableTheLastActiveAdmin()
    {
        await using var db = NewDb();
        var admin = new User { Email = "admin@retrack.vn", Role = "ADMIN", IsActive = true };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        var service = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetUserActiveAsync(admin.Id, false, admin.Id));
        Assert.True((await db.Users.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task SetUserActiveDisablesAdminWhenAnotherActiveAdminRemains()
    {
        await using var db = NewDb();
        var first = new User { Email = "a@retrack.vn", Role = "ADMIN", IsActive = true };
        var second = new User { Email = "b@retrack.vn", Role = "ADMIN", IsActive = true };
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.SetUserActiveAsync(first.Id, false, second.Id);

        Assert.False(result.IsActive);
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "DEACTIVATE" && a.EntityName == "User");
    }

    [Fact]
    public async Task UpdateUserBumpsUpdatedAt()
    {
        await using var db = NewDb();
        var user = new User { Email = "s@retrack.vn", Role = "SELLER", FullName = "Cũ", Phone = "0900000000", UpdatedAt = Utc(2020, 1, 1) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.UpdateUserAsync(user.Id, new UpdateUserDto { FullName = "Mới", Phone = "0911111111" }, Guid.NewGuid());

        Assert.Equal("Mới", result.FullName);
        Assert.True(result.UpdatedAt > Utc(2020, 1, 1));
    }

    [Fact]
    public async Task DeleteUserRefusesWhenDepotDependsOnIt()
    {
        await using var db = NewDb();
        var owner = new User { Email = "depot@retrack.vn", Role = "DEPOT_OWNER" };
        db.Depots.Add(new Depot { Owner = owner, Name = "Kho A", Address = "HCM" });
        await db.SaveChangesAsync();
        var service = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteUserAsync(owner.Id, Guid.NewGuid()));
        Assert.NotNull(await db.Users.FindAsync(owner.Id));
    }

    [Fact]
    public async Task CreateUserRejectsUnknownRole()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var dto = new CreateUserDto { Email = "x@retrack.vn", Password = "Pass@123", FullName = "X", Phone = "0900000000", Role = "SUPER_ADMIN" };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateUserAsync(dto, Guid.NewGuid()));
    }

    // ── Cấu hình phí ──────────────────────────────────────────────

    [Theory]
    [InlineData("-0.5")]
    [InlineData("100.5")]
    [InlineData("101")]
    public async Task UpdateFeeConfigRejectsPercentageOutsideZeroToHundred(string percentage)
    {
        await using var db = NewDb();
        var service = NewService(db);
        var value = decimal.Parse(percentage, CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateFeeConfigAsync(new UpdateFeeConfigDto { PlatformFeePercentage = value }, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateFeeConfigPersistsInvariantDecimalAndIsReadBack()
    {
        await using var db = NewDb();
        var service = NewService(db);

        await service.UpdateFeeConfigAsync(new UpdateFeeConfigDto { PlatformFeePercentage = 2.5m }, Guid.NewGuid());

        var stored = await db.SystemConfigs.SingleAsync(c => c.ConfigKey == "PLATFORM_FEE_PERCENTAGE");
        Assert.Equal(2.5m, decimal.Parse(stored.ConfigValue, CultureInfo.InvariantCulture));
        Assert.DoesNotContain(",", stored.ConfigValue);
        Assert.Equal(2.5m, (await service.GetFeeConfigAsync()).PlatformFeePercentage);
    }

    [Fact]
    public async Task GetFeeConfigFallsBackToOnePercentWhenUnset()
    {
        await using var db = NewDb();

        Assert.Equal(1.00m, (await NewService(db).GetFeeConfigAsync()).PlatformFeePercentage);
    }

    // ── Hóa đơn phí nền tảng ──────────────────────────────────────

    [Fact]
    public async Task GenerateMonthlyInvoicesAggregatesPerDepotOwnerAndSkipsExistingPeriod()
    {
        await using var db = NewDb();
        var depotOwner = new User { Email = "depot@retrack.vn", Role = "DEPOT_OWNER", FullName = "Chủ kho" };
        var seller = new User { Email = "seller@retrack.vn", Role = "SELLER" };
        var depot = new Depot { Owner = depotOwner, Name = "Kho A", Address = "HCM" };
        var first = new PickupRequest { Seller = seller, TargetDepot = depot, Address = "A" };
        var second = new PickupRequest { Seller = seller, TargetDepot = depot, Address = "B" };
        db.PickupRequests.AddRange(first, second);
        await db.SaveChangesAsync();

        db.PlatformTransactions.AddRange(
            Tx(Utc(2026, 1, 10), 30_000m, first.Id),
            Tx(Utc(2026, 1, 20), 20_000m, second.Id),
            Tx(Utc(2026, 2, 5), 99_000m, first.Id));   // ngoài kỳ, không được tính
        await db.SaveChangesAsync();
        var service = NewService(db);

        var created = await service.GenerateMonthlyInvoicesAsync(2026, 1, Guid.NewGuid());

        var invoice = Assert.Single(created);
        Assert.Equal(depotOwner.Id, invoice.PayerId);
        Assert.Equal(50_000m, invoice.TotalFeeAmount);
        Assert.Equal("PENDING", invoice.Status);
        Assert.Equal("Chủ kho", invoice.PayerName);
        Assert.Single(await db.Notifications.Where(n => n.UserId == depotOwner.Id).ToListAsync());

        // Chạy lại cùng kỳ không được tạo hóa đơn trùng
        Assert.Empty(await service.GenerateMonthlyInvoicesAsync(2026, 1, Guid.NewGuid()));
        Assert.Single(await db.PlatformInvoices.ToListAsync());
    }

    [Fact]
    public async Task MarkInvoicePaidStampsPaidAtAndLogs()
    {
        await using var db = NewDb();
        var payer = new User { Email = "p@retrack.vn", Role = "FACTORY", FullName = "Nhà máy" };
        var invoice = new PlatformInvoice { Payer = payer, PeriodYear = 2026, PeriodMonth = 1, TotalFeeAmount = 10_000m };
        db.PlatformInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.MarkInvoicePaidAsync(invoice.Id, Guid.NewGuid());

        Assert.Equal("PAID", result.Status);
        Assert.NotNull(result.PaidAt);
        Assert.Equal("Nhà máy", result.PayerName);
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "MARK_PAID");
    }

    [Fact]
    public async Task ResendInvoiceReminderNotifiesPayer()
    {
        await using var db = NewDb();
        var payer = new User { Email = "p@retrack.vn", Role = "DEPOT_OWNER" };
        var invoice = new PlatformInvoice { Payer = payer, PeriodYear = 2026, PeriodMonth = 1, TotalFeeAmount = 10_000m };
        db.PlatformInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var service = NewService(db);

        await service.ResendInvoiceReminderAsync(invoice.Id);

        Assert.Single(await db.Notifications.Where(n => n.UserId == payer.Id).ToListAsync());
    }

    [Fact]
    public async Task GetTransactionsLeavesPayerNameNullWhenPickupHasNoTargetDepot()
    {
        await using var db = NewDb();
        var seller = new User { Email = "seller@retrack.vn", Role = "SELLER" };
        var orphan = new PickupRequest { Seller = seller, Address = "A" }; // không có kho vựa đích
        db.PickupRequests.Add(orphan);
        await db.SaveChangesAsync();
        db.PlatformTransactions.Add(Tx(Utc(2026, 1, 10), 5_000m, orphan.Id));
        await db.SaveChangesAsync();
        var service = NewService(db);

        var page = await service.GetTransactionsAsync(null, null, null, 1, 20);

        Assert.Null(Assert.Single(page.Items).PayerName);
    }
}
