using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.ESign;
using Xunit;

namespace Retrack.API.Tests;

public sealed class VietnameseNumberTests
{
    [Theory]
    [InlineData(1060000, "Một triệu không trăm sáu mươi nghìn đồng")]
    [InlineData(10600000, "Mười triệu sáu trăm nghìn đồng")]
    [InlineData(9540000, "Chín triệu năm trăm bốn mươi nghìn đồng")]
    [InlineData(1005000000, "Một tỷ không trăm lẻ năm triệu đồng")]
    [InlineData(0, "Không đồng")]
    [InlineData(21500, "Hai mươi mốt nghìn năm trăm đồng")]
    public void Amounts_are_read_like_the_frontend(long amount, string expected) =>
        Assert.Equal(expected, VietnameseNumber.AmountInWords(amount));
}

public sealed class LiquidationEsignPostgresTests : IAsyncLifetime
{
    private readonly string schema = "liq_esign_" + Guid.NewGuid().ToString("N");
    private NpgsqlConnection admin = null!;
    private string connection = null!;
    private readonly Guid seller = Guid.NewGuid(), buyer = Guid.NewGuid(), stranger = Guid.NewGuid(), depotId = Guid.NewGuid(), lotId = Guid.NewGuid(), agreementId = Guid.NewGuid();
    private readonly FakeDocuSign client = new();

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var parsed = new NpgsqlConnectionStringBuilder(source);
        Assert.Equal("Retrack_TV2_test", parsed.Database);
        Assert.Equal("Retrack_TV2_test", new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")).Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        admin = new(source); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public", Pooling = false }.ConnectionString;
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        await using (var setup = new NpgsqlConnection(connection))
        {
            await setup.OpenAsync();
            await using var command = new NpgsqlCommand(sql[..sql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;", setup) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
        await using var db = Open();
        var sellerUser = new User { Id = seller, Role = "SELLER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Seller ký thử", Phone = "0900000001" };
        var buyerUser = new User { Id = buyer, Role = "DEPOT_OWNER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Chủ kho ký thử", Phone = "0900000002" };
        var strangerUser = new User { Id = stranger, Role = "DEPOT_OWNER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Người lạ", Phone = "0900000003" };
        var depot = new Depot { Id = depotId, Owner = buyerUser, Name = "Kho Đà Nẵng", Address = "1 Nguyễn Văn Linh", VerificationStatus = "APPROVED" };
        var now = DateTime.UtcNow;
        var lot = new LiquidationLot
        {
            Id = lotId, SellerId = seller, Title = "Thanh lý khách sạn Đà Nẵng", ScopeDetails = "Toàn bộ nội thất", CollectionTerms = "Thu gom trong 7 ngày",
            Address = "2 Bạch Đằng", ContactPhone = "0900000001", SurveyStart = now.AddDays(-9), SurveyEnd = now.AddDays(-8), BiddingStart = now.AddDays(-5), OriginalEnd = now.AddDays(-1),
            BiddingEnd = now.AddDays(-1), CollectionDeadline = now.AddDays(7), StartingPrice = 1_000_000, BidIncrement = 100_000, DepositPercent = 10,
            HighestPrice = 10_600_000, BidCount = 1, Status = "AWARDED", RequestHash = new string('a', 64)
        };
        var bid = new LiquidationBid { Id = Guid.NewGuid(), LotId = lotId, DepotId = depotId, OwnerId = buyer, Amount = 10_600_000 };
        db.AddRange(sellerUser, strangerUser, depot, lot);
        await db.SaveChangesAsync();
        db.Add(bid); await db.SaveChangesAsync();
        db.Add(new LiquidationAgreement { Id = agreementId, LotId = lotId, WinningBidId = bid.Id, DepotId = depotId, BuyerId = buyer, Amount = 10_600_000, DepositAmount = 1_060_000 });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await drop.ExecuteNonQueryAsync(); await admin.DisposeAsync();
    }

    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private LiquidationEsignService Service(AppDbContext db) => new(db, client, new ConfigurationBuilder().Build(), NullLogger<LiquidationEsignService>.Instance);
    private static EnvelopeState State(string status, bool seller, bool buyer) => new(status, status == "completed" ? DateTime.UtcNow : null,
        [new("1", seller ? "completed" : "sent", seller ? DateTime.UtcNow : null, null), new("2", buyer ? "completed" : "sent", buyer ? DateTime.UtcNow : null, null)]);

    [PostgresEsignFact]
    public async Task Both_signatures_on_docusign_record_signed_times_once_and_complete_the_contract()
    {
        await using var db = Open();
        var url = await Service(db).CreateSigningUrlAsync(seller, agreementId, default);
        Assert.StartsWith("https://example.invalid/sign/", url.Url);
        Assert.Equal(1, client.Created);
        var html = System.Net.WebUtility.HtmlDecode(client.LastHtml);
        Assert.Contains("HỢP ĐỒNG ĐẶT CỌC", html);
        Assert.Contains("Một triệu không trăm sáu mươi nghìn đồng", html);

        client.Reply = State("sent", true, false);
        await using (var d1 = Open())
        {
            var status = await Service(d1).RefreshAsync(seller, agreementId, default);
            Assert.Equal("SELLER_SIGNED", status.State); Assert.True(status.SellerSigned); Assert.False(status.BuyerSigned);
        }
        await using (var d2 = Open())
        {
            Assert.NotNull((await d2.LiquidationAgreements.SingleAsync()).SellerSignedAt);
            Assert.Null((await d2.LiquidationAgreements.SingleAsync()).BuyerSignedAt);
        }
        client.Reply = State("completed", true, true);
        DateTime? sellerAt;
        await using (var d3 = Open())
        {
            var status = await Service(d3).RefreshAsync(buyer, agreementId, default);
            Assert.Equal("COMPLETED", status.State); Assert.True(status.MySideSigned);
            sellerAt = (await d3.LiquidationAgreements.AsNoTracking().SingleAsync()).SellerSignedAt;
        }
        // Đọc lại lần nữa không ghi đè mốc ký và không tạo thêm thông báo.
        await using var d4 = Open();
        var before = await d4.Notifications.CountAsync();
        await Service(d4).RefreshAsync(seller, agreementId, default);
        var a = await d4.LiquidationAgreements.AsNoTracking().SingleAsync();
        Assert.Equal(sellerAt, a.SellerSignedAt); Assert.NotNull(a.BuyerSignedAt); Assert.NotNull(a.EsignCompletedAt);
        Assert.Equal(before, await d4.Notifications.CountAsync());
        var pdf = await Service(d4).DownloadAsync(seller, agreementId, default);
        Assert.StartsWith("HDDC-", pdf.FileName); Assert.True(pdf.Content.Length > 0);
    }

    [PostgresEsignFact]
    public async Task Stranger_cannot_read_sign_or_download_and_legacy_button_is_blocked_once_docusign_is_on()
    {
        await using var db = Open();
        var service = Service(db);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.GetStatusAsync(stranger, agreementId, default));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.CreateSigningUrlAsync(stranger, agreementId, default));
        await Assert.ThrowsAsync<DepotConflictException>(() => service.DownloadAsync(seller, agreementId, default));
        // DocuSign bật và chưa ai ký theo cách cũ: nút xác nhận cũ bị từ chối.
        await Assert.ThrowsAsync<DepotConflictException>(() => service.EnsureLegacySigningAllowedAsync(seller, agreementId, default));
    }

    [PostgresEsignFact]
    public async Task Decline_clears_signatures_and_a_new_envelope_is_created_for_the_next_attempt()
    {
        await using (var d1 = Open()) await Service(d1).CreateSigningUrlAsync(seller, agreementId, default);
        client.Reply = State("sent", true, false);
        await using (var d2 = Open()) await Service(d2).RefreshAsync(seller, agreementId, default);
        client.Reply = State("declined", true, false);
        await using (var d3 = Open())
        {
            var status = await Service(d3).RefreshAsync(buyer, agreementId, default);
            Assert.Equal("DECLINED", status.State); Assert.False(status.SellerSigned);
            Assert.Null((await d3.LiquidationAgreements.AsNoTracking().SingleAsync()).SellerSignedAt);
        }
        await using var d4 = Open();
        await Service(d4).CreateSigningUrlAsync(seller, agreementId, default);
        Assert.Equal(2, client.Created);
    }

    [PostgresEsignFact]
    public async Task Legacy_signature_before_docusign_keeps_the_old_path_open_and_never_creates_an_envelope()
    {
        await using (var seed = Open())
        {
            var a = await seed.LiquidationAgreements.SingleAsync(); a.SellerSignedAt = DateTime.UtcNow; await seed.SaveChangesAsync();
        }
        await using var db = Open();
        await Service(db).EnsureLegacySigningAllowedAsync(seller, agreementId, default);
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).CreateSigningUrlAsync(buyer, agreementId, default));
        Assert.Equal(0, client.Created);
    }

    [PostgresEsignFact]
    public async Task Status_tells_the_ui_when_the_legacy_button_is_still_allowed()
    {
        await using (var d1 = Open()) Assert.False((await Service(d1).GetStatusAsync(seller, agreementId, default)).LegacyAllowed);
        await using (var seed = Open())
        {
            var a = await seed.LiquidationAgreements.SingleAsync(); a.SellerSignedAt = DateTime.UtcNow; await seed.SaveChangesAsync();
        }
        await using var d2 = Open();
        Assert.True((await Service(d2).GetStatusAsync(buyer, agreementId, default)).LegacyAllowed);
        client.Configured = false;
        await using var d3 = Open();
        var off = await Service(d3).GetStatusAsync(buyer, agreementId, default);
        Assert.False(off.Configured); Assert.True(off.LegacyAllowed);
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(d3).CreateSigningUrlAsync(buyer, agreementId, default));
    }

    [PostgresEsignFact]
    public async Task Legacy_gate_checks_membership_first_so_it_does_not_reveal_other_agreements()
    {
        await using var db = Open();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).EnsureLegacySigningAllowedAsync(stranger, agreementId, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).EnsureLegacySigningAllowedAsync(seller, Guid.NewGuid(), default));
    }

    [PostgresEsignFact]
    public async Task Deactivated_account_cannot_use_the_endpoints_even_for_its_own_agreement()
    {
        await using (var seed = Open())
        {
            var u = await seed.Users.SingleAsync(x => x.Id == seller); u.IsActive = false; await seed.SaveChangesAsync();
        }
        await using var db = Open();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).GetStatusAsync(seller, agreementId, default));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).CreateSigningUrlAsync(seller, agreementId, default));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).DownloadAsync(seller, agreementId, default));
        Assert.Equal(0, client.Created);
    }

    [PostgresEsignFact]
    public async Task Replacing_a_voided_envelope_clears_old_signatures_so_both_sign_again()
    {
        await using (var d1 = Open()) await Service(d1).CreateSigningUrlAsync(seller, agreementId, default);
        client.Reply = State("sent", true, false);
        await using (var d2 = Open()) await Service(d2).RefreshAsync(seller, agreementId, default);
        client.Reply = State("voided", true, false);
        await using (var d3 = Open()) Assert.Equal("VOIDED", (await Service(d3).RefreshAsync(seller, agreementId, default)).State);
        client.Reply = State("sent", false, false); // envelope mới chưa ai ký
        await using (var d4 = Open())
        {
            await Service(d4).CreateSigningUrlAsync(seller, agreementId, default); // envelope thay thế: Seller phải ký lại
            Assert.Equal(2, client.Created);
        }
        await using var d5 = Open();
        var a = await d5.LiquidationAgreements.AsNoTracking().SingleAsync();
        Assert.Null(a.SellerSignedAt); Assert.Null(a.BuyerSignedAt); Assert.Equal("SENT", a.EsignStatus);
    }

    [PostgresEsignFact]
    public async Task User_text_cannot_inject_signature_anchors_and_numbers_do_not_depend_on_culture()
    {
        await using (var seed = Open())
        {
            var lot = await seed.LiquidationLots.SingleAsync(); lot.ScopeDetails = "Nội thất /ksb/ và /kss/ khác"; lot.DepositPercent = 10.5m; await seed.SaveChangesAsync();
        }
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        try
        {
            await using var db = Open();
            await Service(db).CreateSigningUrlAsync(seller, agreementId, default);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
        var html = System.Net.WebUtility.HtmlDecode(client.LastHtml);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, "/ksb/").Count);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, "/kss/").Count);
        Assert.Contains("10.5%", html);
        Assert.Matches(@"\d{2}/\d{2}/\d{4}", html);
    }

    [PostgresEsignFact]
    public async Task Webhook_with_a_known_envelope_syncs_signatures_and_unknown_ids_are_ignored()
    {
        await using (var d1 = Open()) await Service(d1).CreateSigningUrlAsync(seller, agreementId, default);
        string envelopeId;
        await using (var peek = Open()) envelopeId = (await peek.LiquidationAgreements.AsNoTracking().SingleAsync()).EsignEnvelopeId!;
        client.Reply = State("completed", true, true);
        await using (var d2 = Open()) await Service(d2).HandleWebhookAsync("{\"envelopeId\":\"" + Guid.NewGuid() + "\"}", default);
        await using (var d3 = Open()) Assert.Null((await d3.LiquidationAgreements.AsNoTracking().SingleAsync()).SellerSignedAt);
        await using (var d4 = Open()) await Service(d4).HandleWebhookAsync("{\"envelopeId\":\"" + envelopeId + "\"}", default);
        await using var d5 = Open();
        var a = await d5.LiquidationAgreements.AsNoTracking().SingleAsync();
        Assert.NotNull(a.SellerSignedAt); Assert.NotNull(a.BuyerSignedAt); Assert.Equal("COMPLETED", a.EsignStatus);
    }

    private sealed class FakeDocuSign : IDocuSignClient
    {
        public bool Configured { get; set; } = true;
        public bool IsConfigured => Configured;
        public int Created { get; private set; }
        public string LastHtml { get; private set; } = "";
        public EnvelopeState Reply { get; set; } = new("sent", null, []);
        public Task<string> CreateEnvelopeAsync(string subject, string documentName, string html, IReadOnlyList<EnvelopeSigner> signers, CancellationToken ct)
        { Created++; LastHtml = html; return Task.FromResult(Guid.NewGuid().ToString()); }
        public Task<string> CreateSigningUrlAsync(string envelopeId, EnvelopeSigner signer, string returnUrl, CancellationToken ct) => Task.FromResult("https://example.invalid/sign/" + envelopeId);
        public Task<EnvelopeState> GetEnvelopeAsync(string envelopeId, CancellationToken ct) => Task.FromResult(Reply);
        public Task<byte[]> DownloadCombinedAsync(string envelopeId, CancellationToken ct) => Task.FromResult("%PDF-TEST"u8.ToArray());
    }
}

public sealed class PostgresEsignFactAttribute : FactAttribute
{
    public PostgresEsignFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL test riêng để kiểm tra ký điện tử hợp đồng thanh lý.";
    }
}
