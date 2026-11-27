using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

/// <summary>
/// Hoàn tiền gói trên PostgreSQL thật (schema tạm từ retrack-system.sql trong Retrack_TV2_test, tự xóa).
/// Kiểm tra: duyệt khác đã chuyển tiền, rút đúng kỳ chưa bắt đầu, kỳ đang dùng không hoàn, giới hạn số tiền, retry/race, chống lách dùng thử.
/// </summary>
public sealed class SubscriptionRefundPostgresTests : IAsyncLifetime
{
    private readonly string schema = "sub_refund_" + Guid.NewGuid().ToString("N");
    private NpgsqlConnection admin = null!;
    private string connection = null!;
    private readonly Guid owner = Guid.NewGuid(), otherOwner = Guid.NewGuid(), adminUser = Guid.NewGuid(), depot = Guid.NewGuid(), otherDepot = Guid.NewGuid();
    private readonly ManualClock clock = new(new DateTime(2026, 1, 10, 3, 0, 0, DateTimeKind.Utc));
    private sealed class NoPayOs : IPayOsClient
    {
        public bool IsConfigured => false;
        public Task<PayOsLink> CreatePaymentLinkAsync(long code, long amount, string description, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PayOsPayment> GetPaymentAsync(long code, CancellationToken ct = default) => throw new NotSupportedException();
        public bool VerifyWebhookData(JsonElement data, string signature) => false;
    }

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(source)) return;
        Assert.Equal("Retrack_TV2_test", new NpgsqlConnectionStringBuilder(source).Database);
        Assert.Equal("Retrack_TV2_test", new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")).Database);
        admin = new(source); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public", Pooling = false }.ConnectionString;
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        await using (var setup = new NpgsqlConnection(connection))
        {
            await setup.OpenAsync();
            await using var command = new NpgsqlCommand(sql[..sql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;", setup) { CommandTimeout = 180 };
            await command.ExecuteNonQueryAsync();
        }
        await using var db = Open();
        db.Users.AddRange(
            new User { Id = owner, Role = "DEPOT_OWNER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Chủ kho" },
            new User { Id = otherOwner, Role = "DEPOT_OWNER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Chủ kho khác" },
            new User { Id = adminUser, Role = "ADMIN", Email = Guid.NewGuid() + "@test.invalid", FullName = "Admin" });
        db.Depots.AddRange(new Depot { Id = depot, OwnerId = owner, Name = "Kho A", VerificationStatus = "APPROVED", TaxCode = "0101234567" },
            new Depot { Id = otherDepot, OwnerId = otherOwner, Name = "Kho B", VerificationStatus = "APPROVED", TaxCode = " 0101234567 " });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE system_configs SET config_value = 'DEPOT_SUBSCRIPTION' WHERE config_key = 'BUSINESS_MODEL'");
    }

    public async Task DisposeAsync()
    {
        if (admin == null) return;
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await drop.ExecuteNonQueryAsync(); await admin.DisposeAsync();
    }

    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private async Task<T> Run<T>(Func<DepotSubscriptionService, Task<T>> action) { await using var db = Open(); return await action(new(new DepotSubscriptionRepository(db), new NoPayOs(), clock)); }
    private async Task Run(Func<DepotSubscriptionService, Task> action) { await using var db = Open(); await action(new(new DepotSubscriptionRepository(db), new NoPayOs(), clock)); }

    /// <summary>Mua một tháng bằng chuyển khoản + Admin đối soát; trả về đơn đã PAID.</summary>
    private async Task<SubscriptionOrderDto> BuyMonthAsync(Guid who, Guid where)
    {
        var order = await Run(s => s.CreateOrderAsync(who, where, Guid.NewGuid()));
        await Run(s => s.SubmitProofAsync(who, where, order.Id, new("https://example.test/proof.png", "CK-" + order.Id.ToString("N")[..6])));
        await Run(s => s.ApproveAsync(adminUser, order.Id, "VCB-" + order.Id.ToString("N")[..6]));
        await using var db = Open();
        var o = await db.DepotSubscriptionOrders.SingleAsync(x => x.Id == order.Id);
        Assert.Equal("PAID", o.Status);
        return order with { PeriodStart = o.PeriodStart, PeriodEnd = o.PeriodEnd };
    }
    private async Task<DateTime?> PaidUntilAsync() { await using var db = Open(); return (await db.DepotSubscriptions.SingleAsync(x => x.DepotId == depot)).PaidUntil; }
    private static SubscriptionRefundRequestDto Req(Guid order, string kind, decimal amount, string? evidence = "VCB-REF-01") =>
        new(Guid.NewGuid(), order, kind, amount, "Lý do hoàn tiền đủ dài để Admin đối soát", null, evidence);

    [PostgresRefundFact]
    public async Task Public_plan_reflects_mode_price_and_trial_without_login()
    {
        var plan = await Run(s => s.PublicPlanAsync());
        Assert.Equal(new PublicDepotPlanDto(true, 199000m, 14), plan);
        await Run(s => s.SetPriceAsync(adminUser, 249000m));
        Assert.Equal(249000m, (await Run(s => s.PublicPlanAsync())).MonthlyPrice);
        await using (var db = Open()) await db.Database.ExecuteSqlRawAsync("UPDATE system_configs SET config_value = 'PLATFORM_MONEY' WHERE config_key = 'BUSINESS_MODEL'");
        Assert.False((await Run(s => s.PublicPlanAsync())).SubscriptionOnly);
    }

    [PostgresRefundFact]
    public async Task Unused_future_period_is_refunded_without_touching_current_period()
    {
        var first = await BuyMonthAsync(owner, depot);
        var second = await BuyMonthAsync(owner, depot);
        Assert.Equal(first.PeriodEnd, second.PeriodStart);
        Assert.Equal(second.PeriodEnd, await PaidUntilAsync());

        // Kỳ đang sử dụng không được hoàn vì ngừng sử dụng.
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.RequestRefundAsync(owner, depot, Req(first.Id, "UNUSED_PERIOD", 199000))));
        var request = await Run(s => s.RequestRefundAsync(owner, depot, Req(second.Id, "UNUSED_PERIOD", 199000)));
        Assert.Equal("REQUESTED", request.Status);
        // Một yêu cầu mở cho mỗi đơn.
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.RequestRefundAsync(owner, depot, Req(second.Id, "SERVICE_ERROR", 1000))));
        // Hạn dùng chưa đổi khi mới chỉ yêu cầu.
        Assert.Equal(second.PeriodEnd, await PaidUntilAsync());

        var approved = await Run(s => s.DecideRefundAsync(adminUser, request.Id, new(true, null, "Kỳ chưa bắt đầu, đồng ý hoàn")));
        Assert.Equal("APPROVED", approved.Status); Assert.True(approved.PeriodRevoked);
        Assert.Equal(first.PeriodEnd, await PaidUntilAsync()); // chỉ rút đúng kỳ thứ hai
        // Duyệt chưa phải đã chuyển tiền: doanh thu ròng chưa trừ.
        var mid = await Run(s => s.AdminSummaryAsync(adminUser));
        Assert.Equal(398000, mid.PaidRevenue); Assert.Equal(0, mid.RefundedAmount); Assert.Equal(199000, mid.ApprovedAwaitingTransfer);

        var done = await Run(s => s.MarkRefundTransferredAsync(adminUser, request.Id, new("VCB-HOAN-001")));
        var retry = await Run(s => s.MarkRefundTransferredAsync(adminUser, request.Id, new("VCB-HOAN-001")));
        Assert.Equal("REFUNDED", done.Status); Assert.Equal(done.RefundedAt, retry.RefundedAt);
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.MarkRefundTransferredAsync(adminUser, request.Id, new("MA-KHAC"))));
        var summary = await Run(s => s.AdminSummaryAsync(adminUser));
        Assert.Equal(199000, summary.RefundedAmount); Assert.Equal(199000, summary.NetRevenue);

        await using var db = Open();
        Assert.True(await db.AuditLogs.CountAsync(x => x.EntityId == request.Id) >= 3);
        Assert.NotNull((await db.DepotSubscriptionOrders.SingleAsync(x => x.Id == second.Id)).PeriodRevokedAt);
    }

    [PostgresRefundFact]
    public async Task Only_last_future_period_can_be_refunded_first()
    {
        await BuyMonthAsync(owner, depot);
        var second = await BuyMonthAsync(owner, depot);
        var third = await BuyMonthAsync(owner, depot);
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.RequestRefundAsync(owner, depot, Req(second.Id, "UNUSED_PERIOD", 199000))));
        var last = await Run(s => s.RequestRefundAsync(owner, depot, Req(third.Id, "UNUSED_PERIOD", 199000)));
        await Run(s => s.DecideRefundAsync(adminUser, last.Id, new(true, null, "Hoàn kỳ cuối")));
        Assert.Equal(second.PeriodEnd, await PaidUntilAsync());
        // Kỳ thứ hai giờ là kỳ cuối nên được hoàn tiếp.
        var next = await Run(s => s.RequestRefundAsync(owner, depot, Req(second.Id, "UNUSED_PERIOD", 199000)));
        Assert.Equal("REQUESTED", next.Status);
    }

    [PostgresRefundFact]
    public async Task Amount_caps_evidence_permissions_and_rejection_are_enforced()
    {
        var order = await BuyMonthAsync(owner, depot);
        // Chuyển trùng cần chứng từ/mã giao dịch.
        await Assert.ThrowsAsync<ArgumentException>(() => Run(s => s.RequestRefundAsync(owner, depot, Req(order.Id, "DUPLICATE_OR_EXCESS", 199000, null))));
        // Chủ kho khác không yêu cầu được trên đơn của kho này.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(s => s.RequestRefundAsync(otherOwner, otherDepot, Req(order.Id, "SERVICE_ERROR", 1000))));
        // Lỗi dịch vụ không vượt giá đơn.
        await Assert.ThrowsAsync<ArgumentException>(() => Run(s => s.RequestRefundAsync(owner, depot, Req(order.Id, "SERVICE_ERROR", 199001))));

        var partial = await Run(s => s.RequestRefundAsync(owner, depot, Req(order.Id, "SERVICE_ERROR", 150000)));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(s => s.DecideRefundAsync(adminUser, partial.Id, new(true, 160000, "Vượt số yêu cầu"))));
        await Run(s => s.DecideRefundAsync(adminUser, partial.Id, new(true, 100000, "Xác minh lỗi 2 ngày, hoàn một phần")));
        // Phần còn lại theo giá gói chỉ còn 99.000 đ.
        await Assert.ThrowsAsync<ArgumentException>(() => Run(s => s.RequestRefundAsync(owner, depot, Req(order.Id, "SERVICE_ERROR", 100000))));
        var dup = await Run(s => s.RequestRefundAsync(owner, depot, Req(order.Id, "DUPLICATE_OR_EXCESS", 199000)));
        var rejected = await Run(s => s.DecideRefundAsync(adminUser, dup.Id, new(false, null, "Sao kê không thấy khoản chuyển trùng")));
        Assert.Equal("REJECTED", rejected.Status);
        Assert.Equal("REJECTED", (await Run(s => s.DecideRefundAsync(adminUser, dup.Id, new(false, null, "Gửi lại cùng quyết định")))).Status);
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.MarkRefundTransferredAsync(adminUser, dup.Id, new("KHONG-DUOC"))));
        // Hạn dùng không đổi với hoàn lỗi dịch vụ.
        Assert.Equal(order.PeriodEnd, await PaidUntilAsync());
    }

    [PostgresRefundFact]
    public async Task Short_payment_refund_closes_pending_order_so_a_new_order_can_be_created()
    {
        var pending = await Run(s => s.CreateOrderAsync(owner, depot, Guid.NewGuid()));
        await Run(s => s.SubmitProofAsync(owner, depot, pending.Id, new("https://example.test/thieu.png", "CK-THIEU")));
        var refund = await Run(s => s.RequestRefundAsync(adminUser, null, Req(pending.Id, "SHORT_PAYMENT", 100000)));
        Assert.Equal("ADMIN", refund.RequestedRole);
        await Run(s => s.DecideRefundAsync(adminUser, refund.Id, new(true, null, "Khách chuyển thiếu, hoàn khoản đã nhận")));
        await using (var db = Open()) Assert.Equal("CANCELLED", (await db.DepotSubscriptionOrders.SingleAsync(x => x.Id == pending.Id)).Status);
        var fresh = await Run(s => s.CreateOrderAsync(owner, depot, Guid.NewGuid()));
        Assert.NotEqual(pending.Id, fresh.Id);
        // Đơn đã hủy chỉ còn hoàn tiền về muộn, không kích hoạt.
        var late = await Run(s => s.RequestRefundAsync(owner, depot, Req(pending.Id, "LATE_PAYMENT", 99000)));
        Assert.Equal("REQUESTED", late.Status);
    }

    [PostgresRefundFact]
    public async Task Concurrent_requests_on_one_order_create_a_single_open_request()
    {
        var order = await BuyMonthAsync(owner, depot);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            try { await Run(s => s.RequestRefundAsync(owner, depot, Req(order.Id, "SERVICE_ERROR", 50000))); return true; }
            catch (DepotConflictException) { return false; }
        })));
        Assert.Equal(1, results.Count(x => x));
        await using var db = Open();
        Assert.Equal(1, await db.DepotSubscriptionRefunds.CountAsync(x => x.OrderId == order.Id));
    }

    [PostgresRefundFact]
    public async Task Trial_is_once_per_tax_code_across_depots()
    {
        await Run(s => s.StartTrialAsync(owner, depot));
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.StartTrialAsync(otherOwner, otherDepot)));
        // Kho khác mã số thuế vẫn dùng thử bình thường.
        await using (var db = Open())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE depots SET tax_code = '0309999999' WHERE id = {0}", otherDepot);
        }
        Assert.Equal("TRIAL", (await Run(s => s.StartTrialAsync(otherOwner, otherDepot))).Status);
    }
}

public sealed class PostgresRefundFactAttribute : FactAttribute
{
    public PostgresRefundFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL test riêng (Retrack_TV2_test) để kiểm tra hoàn tiền gói.";
    }
}
