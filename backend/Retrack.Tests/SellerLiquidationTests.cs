using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.DTOs.Seller;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Liquidation;
using Retrack.API.Services.Seller;
using Xunit;
namespace Retrack.Tests;

// Phía Seller của lô thanh lý: tạo lô, hủy, xem bid và xác nhận thỏa thuận. Dùng PostgreSQL thật.
public sealed class SellerLiquidationTests : IAsyncLifetime
{
    private readonly Guid seller = Guid.NewGuid(), stranger = Guid.NewGuid(), owner = Guid.NewGuid(), rival = Guid.NewGuid(), depot = Guid.NewGuid(), rivalDepot = Guid.NewGuid();
    private readonly TestClock clock = new(DateTime.UtcNow);
    private sealed class TestClock(DateTime now) : TimeProvider { public DateTime Now = now; public override DateTimeOffset GetUtcNow() => new(Now); }
    private static AppDbContext Open(params IInterceptor[] interceptors)
    {
        var cs = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")!;
        if (new NpgsqlConnectionStringBuilder(cs).Database != "Retrack_TV2_test") throw new InvalidOperationException("Sai DB test");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).AddInterceptors(interceptors).Options);
    }
    private static DepotService Scope(AppDbContext db) => new(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
    private SellerLiquidationService Seller(AppDbContext db) => new(new SellerLiquidationRepository(db), new DepotUnitOfWork(db), new DepotBatchRepository(db), clock);
    private LiquidationService Depot(AppDbContext db) => new(new LiquidationRepository(db), Scope(db), new DepotUnitOfWork(db), new DepotBatchRepository(db), clock);

    public async Task InitializeAsync()
    {
        await using var db = Open(); await TestDatabase.MigrateAsync(db);
        db.Users.AddRange(
            new User { Id = seller, Email = $"sl-{seller}@test.invalid", Role = "SELLER" }, new User { Id = stranger, Email = $"sl-{stranger}@test.invalid", Role = "SELLER" },
            new User { Id = owner, Email = $"sl-{owner}@test.invalid", Role = "DEPOT_OWNER" }, new User { Id = rival, Email = $"sl-{rival}@test.invalid", Role = "DEPOT_OWNER" });
        db.Depots.AddRange(new Depot { Id = depot, OwnerId = owner, Name = "Kho A", VerificationStatus = "APPROVED" }, new Depot { Id = rivalDepot, OwnerId = rival, Name = "Kho B", VerificationStatus = "APPROVED" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open();
        var users = new[] { seller, stranger, owner, rival };
        await db.Notifications.Where(x => users.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.LiquidationAgreements.Where(x => x.Lot.SellerId == seller || x.Lot.SellerId == stranger).ExecuteDeleteAsync();
        await db.LiquidationLots.Where(x => x.SellerId == seller || x.SellerId == stranger).ExecuteDeleteAsync();
        await db.Depots.Where(x => x.Id == depot || x.Id == rivalDepot).ExecuteDeleteAsync();
        await db.Users.Where(x => users.Contains(x.Id)).ExecuteDeleteAsync();
    }

    // Lịch mặc định: đang khảo sát, mở trả giá sau 2 giờ, đóng sau 3 giờ.
    private CreateLiquidationLotDto Dto(Action<CreateLiquidationLotDto>? tweak = null)
    {
        var dto = new CreateLiquidationLotDto
        {
            OperationId = Guid.NewGuid(), Title = "Thanh lý khách sạn Đà Nẵng", Description = "Tài sản thanh lý, tự khảo sát hiện trạng.",
            ScopeDetails = "Khoảng 1.000 kg sắt và 10 ghế còn dùng được.", CollectionTerms = "Trả giá trọn lô, thu gom ngoài hệ thống.",
            PublicArea = "Ngũ Hành Sơn", Address = "12 Châu Thị Vĩnh Tế, Đà Nẵng", ContactPhone = "0900000000", ImageUrls = ["https://example.com/a.jpg"],
            SurveyStart = clock.Now.AddMinutes(-1), SurveyEnd = clock.Now.AddHours(1), BiddingStart = clock.Now.AddHours(2), BiddingEnd = clock.Now.AddHours(3),
            CollectionDeadline = clock.Now.AddDays(3), StartingPrice = 10_000_000, BidIncrement = 100_000, DepositPercent = 10
        };
        tweak?.Invoke(dto); return dto;
    }
    private async Task<Guid> CreateLot(AppDbContext db, Guid? by = null)
    {
        var dto = Dto(); await Seller(db).CreateAsync(by ?? seller, dto); return dto.OperationId;
    }
    private async Task Join(AppDbContext db, Guid lot, Guid who, Guid warehouse)
    { await Depot(db).ParticipateAsync(who, warehouse, lot, true, false); await Depot(db).ParticipateAsync(who, warehouse, lot, true, true); }
    // Tạo lô → hai kho đăng ký → bid → chốt phiên thành AWARDED cho kho A.
    private async Task<(Guid Lot, Guid Agreement)> AwardedLot(AppDbContext db)
    {
        var lot = await CreateLot(db); await Join(db, lot, owner, depot);
        clock.Now = clock.Now.AddHours(2.5);
        await Depot(db).BidAsync(owner, depot, lot, new(Guid.NewGuid(), 10_000_000));
        clock.Now = clock.Now.AddHours(1); await Depot(db).FinalizeAsync(lot);
        return (lot, (await db.LiquidationAgreements.SingleAsync(x => x.LotId == lot)).Id);
    }

    [Fact]
    public async Task Create_is_idempotent_and_rejects_conflicting_reuse_of_operation_id()
    {
        await using var db = Open(); var dto = Dto();
        var first = await Seller(db).CreateAsync(seller, dto);
        Assert.Equal(dto.OperationId, first.Lot.Id); Assert.Equal("PUBLISHED", first.Lot.Status); Assert.Equal("SURVEY", first.Lot.Phase); Assert.True(first.CanCancel);
        Assert.Equal(first.Lot.Id, (await Seller(db).CreateAsync(seller, dto)).Lot.Id);
        Assert.Equal(1, await db.LiquidationLots.CountAsync(x => x.Id == dto.OperationId));
        var notice = Assert.Single(await db.Notifications.Where(n => n.UserId == seller && n.TargetId == dto.OperationId).ToListAsync());
        Assert.Equal("LIQUIDATION", notice.Category); Assert.Equal("LIQUIDATION", notice.TargetType);
        dto.StartingPrice = 12_000_000;
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).CreateAsync(seller, dto));
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).CreateAsync(stranger, Dto(x => x.OperationId = dto.OperationId)));
    }

    [Fact]
    public async Task Concurrent_creates_with_the_same_operation_id_make_one_lot()
    {
        var dto = Dto();
        async Task<Guid> Create() { await using var db = Open(); return (await Seller(db).CreateAsync(seller, dto)).Lot.Id; }
        Assert.Single((await Task.WhenAll(Create(), Create(), Create())).Distinct());
        await using var check = Open(); Assert.Equal(1, await check.LiquidationLots.CountAsync(x => x.SellerId == seller));
        Assert.Single(await check.Notifications.Where(n => n.UserId == seller && n.TargetId == dto.OperationId).ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_text_that_is_too_short_after_trimming()
    {
        await using var db = Open();
        await Assert.ThrowsAsync<ArgumentException>(() => Seller(db).CreateAsync(seller, Dto(x => x.ScopeDetails = "   abc       ")));
        Assert.False(await db.LiquidationLots.AnyAsync(x => x.SellerId == seller));
    }

    [Theory]
    [InlineData("price")] [InlineData("increment")] [InlineData("fraction")] [InlineData("order")] [InlineData("past")] [InlineData("short")]
    [InlineData("collect")] [InlineData("deposit0")] [InlineData("deposit51")] [InlineData("depositfraction")] [InlineData("image")] [InlineData("unspecified")] [InlineData("emptyop")]
    public async Task Create_rejects_invalid_input(string kind)
    {
        await using var db = Open();
        var dto = Dto(x =>
        {
            switch (kind)
            {
                case "price": x.StartingPrice = 0; break;
                case "increment": x.BidIncrement = -1; break;
                case "fraction": x.StartingPrice = 10_000_000.5m; break;
                case "order": x.BiddingStart = x.BiddingEnd.AddMinutes(1); break;
                case "past": x.SurveyStart = clock.Now.AddDays(-1); break;
                case "short": x.BiddingEnd = x.BiddingStart.AddMinutes(5); x.CollectionDeadline = x.BiddingEnd.AddDays(1); break;
                case "collect": x.CollectionDeadline = x.BiddingEnd.AddMinutes(5); break;
                case "deposit0": x.DepositPercent = 0; break;
                case "deposit51": x.DepositPercent = 51; break;
                case "depositfraction": x.DepositPercent = 10.123m; break;
                case "image": x.ImageUrls = ["javascript:alert(1)"]; break;
                case "unspecified": x.BiddingEnd = DateTime.SpecifyKind(x.BiddingEnd, DateTimeKind.Unspecified); break;
                case "emptyop": x.OperationId = Guid.Empty; break;
            }
        });
        await Assert.ThrowsAsync<ArgumentException>(() => Seller(db).CreateAsync(seller, dto));
        Assert.False(await db.LiquidationLots.AnyAsync(x => x.SellerId == seller));
    }

    [Fact]
    public async Task Create_blocks_inactive_seller()
    {
        await using var db = Open();
        await db.Users.Where(x => x.Id == seller).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Seller(db).CreateAsync(seller, Dto()));
    }

    [Fact]
    public async Task Seller_only_sees_and_changes_own_lots()
    {
        await using var db = Open(); var mine = await CreateLot(db); var theirs = await CreateLot(db, stranger);
        var list = await Seller(db).ListAsync(seller, new());
        Assert.Equal(mine, Assert.Single(list.Items).Id); Assert.Equal(1, list.TotalCount);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Seller(db).DetailAsync(seller, theirs));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Seller(db).BidsAsync(seller, theirs, 1));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Seller(db).CancelAsync(seller, theirs, "Không muốn bán nữa"));
        Assert.Equal("PUBLISHED", (await db.LiquidationLots.SingleAsync(x => x.Id == theirs)).Status);
        await Assert.ThrowsAsync<ArgumentException>(() => Seller(db).ListAsync(seller, new() { Status = "HACK" }));
        Assert.Empty((await Seller(db).ListAsync(seller, new() { Status = "AWARDED" })).Items);
        Assert.Single((await Seller(db).ListAsync(seller, new() { Status = "SURVEY" })).Items);
    }

    [Fact]
    public async Task Cancel_without_bids_notifies_participants_and_is_retry_safe()
    {
        await using var db = Open(); var lot = await CreateLot(db); await Join(db, lot, owner, depot);
        await Assert.ThrowsAsync<ArgumentException>(() => Seller(db).CancelAsync(seller, lot, "x"));
        await Seller(db).CancelAsync(seller, lot, "Đổi kế hoạch thanh lý"); await Seller(db).CancelAsync(seller, lot, "Đổi kế hoạch thanh lý");
        var row = await db.LiquidationLots.AsNoTracking().SingleAsync(x => x.Id == lot);
        Assert.Equal("CANCELLED", row.Status); Assert.Equal("Đổi kế hoạch thanh lý", row.CancelReason);
        Assert.Single(await db.Notifications.Where(x => x.UserId == owner && x.TargetId == lot).ToListAsync());
        Assert.Single(await db.Notifications.Where(x => x.UserId == seller && x.TargetId == lot && x.Title == "Đã hủy lô thanh lý").ToListAsync());
        clock.Now = clock.Now.AddHours(2.5);
        await Assert.ThrowsAsync<DepotConflictException>(() => Depot(db).BidAsync(owner, depot, lot, new(Guid.NewGuid(), 10_000_000)));
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).CancelAsync(seller, lot, "Lý do khác hẳn"));
    }

    [Fact]
    public async Task Cancel_is_refused_once_a_bid_exists_and_bids_are_ranked_for_seller()
    {
        await using var db = Open(); var lot = await CreateLot(db); await Join(db, lot, owner, depot); await Join(db, lot, rival, rivalDepot);
        clock.Now = clock.Now.AddHours(2.5);
        await Depot(db).BidAsync(owner, depot, lot, new(Guid.NewGuid(), 10_000_000));
        await Depot(db).BidAsync(rival, rivalDepot, lot, new(Guid.NewGuid(), 10_100_000));
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).CancelAsync(seller, lot, "Không muốn bán nữa"));
        var bids = await Seller(db).BidsAsync(seller, lot, 1);
        Assert.Equal([10_100_000m, 10_000_000m], bids.Items.Select(x => x.Amount));
        Assert.Equal(["Kho B", "Kho A"], bids.Items.Select(x => x.DepotName)); Assert.True(bids.Items[0].Leading); Assert.False(bids.Items[1].Leading);
        var detail = await Seller(db).DetailAsync(seller, lot);
        Assert.Equal(10_100_000m, detail.Lot.HighestPrice); Assert.Equal(2, detail.Lot.BidCount); Assert.Equal(2, detail.Lot.ParticipantCount); Assert.False(detail.CanCancel);
    }

    [Fact]
    public async Task Cancel_and_bid_race_never_leaves_a_cancelled_lot_with_a_bid()
    {
        Guid lot;
        await using (var setup = Open()) { lot = await CreateLot(setup); await Join(setup, lot, owner, depot); }
        clock.Now = clock.Now.AddHours(2.5);
        async Task<bool> Cancel() { await using var db = Open(); try { await Seller(db).CancelAsync(seller, lot, "Huỷ vì đổi kế hoạch"); return true; } catch (DepotConflictException) { return false; } }
        async Task<bool> Bid() { await using var db = Open(); try { await Depot(db).BidAsync(owner, depot, lot, new(Guid.NewGuid(), 10_000_000)); return true; } catch (DepotConflictException) { return false; } }
        var results = await Task.WhenAll(Cancel(), Bid());
        await using var check = Open(); var row = await check.LiquidationLots.AsNoTracking().SingleAsync(x => x.Id == lot);
        Assert.NotEqual(results[0], results[1]); // đúng một bên thắng
        Assert.Equal(results[0] ? "CANCELLED" : "PUBLISHED", row.Status); Assert.Equal(results[1] ? 1 : 0, row.BidCount);
    }

    [Fact]
    public async Task Agreement_flow_follows_payment_report_then_seller_confirmation_then_handover()
    {
        await using var db = Open(); var (lot, agreementId) = await AwardedLot(db);
        var detail = await Seller(db).DetailAsync(seller, lot);
        Assert.Equal("AWARDED", detail.Lot.Status); Assert.Equal("WAITING_SIGNATURES", detail.Agreement!.Stage); Assert.Equal("Kho A", detail.Agreement.DepotName);
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).ConfirmPaymentAsync(seller, agreementId)); // chưa có báo chuyển tiền
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).ConfirmHandoverAsync(seller, agreementId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Seller(db).ConfirmPaymentAsync(stranger, agreementId));
        await SettleDeposit(db, agreementId); Assert.Equal("WAITING_BUYER_PAYMENT", (await Seller(db).DetailAsync(seller, lot)).Agreement!.Stage);
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_PAYMENT", "CK-001");
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_PAYMENT", "CK-001");
        await Assert.ThrowsAsync<DepotConflictException>(() => Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_PAYMENT", "CK-khac"));
        Assert.Single(await db.Notifications.Where(n => n.UserId == seller && n.TargetId == lot && n.Title == "Chủ kho báo đã chuyển tiền thanh toán").ToListAsync());
        Assert.Equal("WAITING_SELLER_CONFIRM", (await Seller(db).DetailAsync(seller, lot)).Agreement!.Stage);
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).ConfirmHandoverAsync(seller, agreementId)); // chưa xác nhận nhận tiền
        var paid = await Seller(db).ConfirmPaymentAsync(seller, agreementId); var again = await Seller(db).ConfirmPaymentAsync(seller, agreementId);
        Assert.Equal(paid.PaymentConfirmedAt, again.PaymentConfirmedAt); Assert.Equal("WAITING_HANDOVER", paid.Stage);
        Assert.Single(await db.Notifications.Where(x => x.UserId == owner && x.Title == "Seller đã xác nhận nhận tiền").ToListAsync());
        var handed = await Seller(db).ConfirmHandoverAsync(seller, agreementId); await Seller(db).ConfirmHandoverAsync(seller, agreementId);
        Assert.Equal("WAITING_OWNER_COLLECT", handed.Stage); Assert.Single(await db.Notifications.Where(x => x.UserId == owner && x.Title == "Seller đã xác nhận bàn giao").ToListAsync());
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "CONFIRM_COLLECTED"); // phía Chủ kho đi tiếp được sau khi Seller xác nhận
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "CONFIRM_COLLECTED");
        Assert.Single(await db.Notifications.Where(n => n.UserId == seller && n.TargetId == lot && n.Title == "Chủ kho đã xác nhận thu gom").ToListAsync());
        Assert.Equal("COMPLETED", (await Seller(db).DetailAsync(seller, lot)).Agreement!.Stage);
    }

    // Hai bên ký, Chủ kho báo chuyển cọc, Seller xác nhận nhận cọc.
    private async Task SettleDeposit(AppDbContext db, Guid agreementId)
    {
        await Seller(db).SignAsync(seller, agreementId); await Depot(db).AgreementActionAsync(owner, depot, agreementId, "SIGN");
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_DEPOSIT", "COC-001"); await Seller(db).ConfirmDepositAsync(seller, agreementId);
    }

    [Fact]
    public async Task Deposit_is_fixed_from_the_winning_price_and_gates_payment_in_order()
    {
        await using var db = Open(); var (lot, agreementId) = await AwardedLot(db);
        var agreement = (await Seller(db).DetailAsync(seller, lot)).Agreement!;
        Assert.Equal(10m, agreement.DepositPercent); Assert.Equal(1_000_000m, agreement.DepositAmount); Assert.Equal(9_000_000m, agreement.RemainingAmount);
        Assert.Equal(1_000_000m, (await Depot(db).AgreementAsync(owner, depot, agreementId)).DepositAmount);
        Assert.Equal("Kho A", agreement.Parties.DepotName); Assert.Equal("Kho A", (await Depot(db).AgreementAsync(owner, depot, agreementId)).Parties.DepotName);
        // Chưa ký thì chưa báo cọc, chưa xác nhận cọc, chưa báo phần còn lại.
        await Assert.ThrowsAsync<DepotConflictException>(() => Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_DEPOSIT", "COC"));
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).ConfirmDepositAsync(seller, agreementId));
        await Assert.ThrowsAsync<DepotConflictException>(() => Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_PAYMENT", "CK"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Seller(db).SignAsync(stranger, agreementId));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Depot(db).AgreementActionAsync(competitor(), rivalDepot, agreementId, "SIGN"));
        var first = await Seller(db).SignAsync(seller, agreementId); var again = await Seller(db).SignAsync(seller, agreementId);
        Assert.Equal(first.SellerSignedAt, again.SellerSignedAt); Assert.Equal("WAITING_SIGNATURES", again.Stage);
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "SIGN"); await Depot(db).AgreementActionAsync(owner, depot, agreementId, "SIGN");
        Assert.Equal("WAITING_DEPOSIT", (await Seller(db).DetailAsync(seller, lot)).Agreement!.Stage);
        await Assert.ThrowsAsync<DepotConflictException>(() => Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_PAYMENT", "CK")); // cọc chưa xong
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_DEPOSIT", "COC-9"); await Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_DEPOSIT", "COC-9");
        await Assert.ThrowsAsync<DepotConflictException>(() => Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_DEPOSIT", "COC-khác"));
        Assert.Equal("WAITING_DEPOSIT_CONFIRM", (await Seller(db).DetailAsync(seller, lot)).Agreement!.Stage);
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).ConfirmPaymentAsync(seller, agreementId));
        await Seller(db).ConfirmDepositAsync(seller, agreementId);
        Assert.Equal("WAITING_BUYER_PAYMENT", (await Seller(db).DetailAsync(seller, lot)).Agreement!.Stage);
        await Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_PAYMENT", "CK-1");
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == seller && x.Title == "Chủ kho báo đã chuyển tiền cọc"));
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == owner && x.Title == "Seller đã xác nhận nhận tiền cọc"));
    }

    [Fact]
    public async Task Price_ladder_is_anonymous_ranked_and_limited_to_registered_warehouses()
    {
        await using var db = Open(); var lot = await CreateLot(db); await Join(db, lot, owner, depot); await Join(db, lot, rival, rivalDepot);
        clock.Now = clock.Now.AddHours(2.5);
        await Depot(db).BidAsync(owner, depot, lot, new(Guid.NewGuid(), 10_000_000)); await Depot(db).BidAsync(rival, rivalDepot, lot, new(Guid.NewGuid(), 10_200_000));
        await Depot(db).BidAsync(owner, depot, lot, new(Guid.NewGuid(), 10_300_000));
        var ladder = await Depot(db).LadderAsync(owner, depot, lot, 1);
        Assert.Equal([10_300_000m, 10_200_000m, 10_000_000m], ladder.Items.Select(x => x.Amount)); Assert.Equal([1, 2, 3], ladder.Items.Select(x => x.Rank));
        Assert.Equal([true, false, true], ladder.Items.Select(x => x.Mine)); Assert.Equal([true, false, false], ladder.Items.Select(x => x.Leading));
        Assert.Equal([false, true, false], (await Depot(db).LadderAsync(rival, rivalDepot, lot, 1)).Items.Select(x => x.Mine));
        var outsider = Guid.NewGuid(); var outsiderDepot = Guid.NewGuid();
        db.Users.Add(new User { Id = outsider, Email = $"sl-{outsider}@test.invalid", Role = "DEPOT_OWNER" });
        db.Depots.Add(new Depot { Id = outsiderDepot, OwnerId = outsider, Name = "Kho ngoài", VerificationStatus = "APPROVED" }); await db.SaveChangesAsync();
        try { await Assert.ThrowsAsync<DepotForbiddenException>(() => Depot(db).LadderAsync(outsider, outsiderDepot, lot, 1)); }
        finally { await db.Depots.Where(x => x.Id == outsiderDepot).ExecuteDeleteAsync(); await db.Users.Where(x => x.Id == outsider).ExecuteDeleteAsync(); }
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Depot(db).LadderAsync(owner, rivalDepot, lot, 1));
    }

    [Fact]
    public async Task Legacy_lot_without_deposit_keeps_the_old_flow()
    {
        await using var db = Open(); var lot = await CreateLot(db); await Join(db, lot, owner, depot);
        await db.LiquidationLots.Where(x => x.Id == lot).ExecuteUpdateAsync(s => s.SetProperty(x => x.DepositPercent, 0m));
        clock.Now = clock.Now.AddHours(2.5); await Depot(db).BidAsync(owner, depot, lot, new(Guid.NewGuid(), 10_000_000));
        clock.Now = clock.Now.AddHours(1); await Depot(db).FinalizeAsync(lot);
        var agreement = (await Seller(db).DetailAsync(seller, lot)).Agreement!; Assert.Equal(0m, agreement.DepositAmount); Assert.Equal("WAITING_BUYER_PAYMENT", agreement.Stage);
        await Assert.ThrowsAsync<DepotConflictException>(() => Seller(db).SignAsync(seller, agreement.Id));
        await Depot(db).AgreementActionAsync(owner, depot, agreement.Id, "REPORT_PAYMENT", "CK-legacy");
    }

    private Guid competitor() => rival;

    [Fact]
    public async Task Creating_lot_rolls_back_when_notification_cannot_be_saved()
    {
        var dto = Dto();
        await using (var db = Open(new FailNoticeSave()))
            await Assert.ThrowsAsync<NoticeSaveException>(() => Seller(db).CreateAsync(seller, dto));
        await using var verify = Open();
        Assert.False(await verify.LiquidationLots.AnyAsync(x => x.Id == dto.OperationId));
        Assert.False(await verify.Notifications.AnyAsync(x => x.TargetId == dto.OperationId));
    }

    private sealed class NoticeSaveException : Exception { }
    private sealed class FailNoticeSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Notification>().Any(x => x.State == EntityState.Added)) throw new NoticeSaveException();
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Seller_notices_follow_registration_bid_and_closing_and_survive_reload_without_duplicates()
    {
        Guid lot;
        await using (var db = Open())
        {
            lot = await CreateLot(db);
            await Join(db, lot, owner, depot);
            await Join(db, lot, owner, depot);
            clock.Now = clock.Now.AddHours(2.5);
            var bid = new LiquidationBidInput(Guid.NewGuid(), 10_000_000);
            await Depot(db).BidAsync(owner, depot, lot, bid);
            await Depot(db).BidAsync(owner, depot, lot, bid);
            clock.Now = clock.Now.AddHours(1);
            await Depot(db).FinalizeAsync(lot);
            await Depot(db).FinalizeAsync(lot);
        }
        await using var verify = Open();
        var notices = await verify.Notifications.Where(n => n.UserId == seller && n.TargetId == lot).ToListAsync();
        Assert.Equal(5, notices.Count);
        Assert.All(notices, n => { Assert.Equal("LIQUIDATION", n.Category); Assert.Equal("LIQUIDATION", n.TargetType); Assert.False(n.IsRead); });
        Assert.Contains(notices, n => n.Title == "Lô thanh lý đã có kho trúng giá");
        Assert.False(await verify.Notifications.AnyAsync(n => n.UserId == stranger && n.TargetId == lot));
    }

    [Fact]
    public async Task No_sale_notifies_seller_even_without_participants_and_retry_does_not_duplicate()
    {
        await using var db = Open(); var lot = await CreateLot(db);
        clock.Now = clock.Now.AddHours(4);
        await Depot(db).FinalizeAsync(lot); await Depot(db).FinalizeAsync(lot);
        await using var verify = Open();
        Assert.Equal("NO_SALE", (await verify.LiquidationLots.SingleAsync(x => x.Id == lot)).Status);
        Assert.Single(await verify.Notifications.Where(n => n.UserId == seller && n.TargetId == lot && n.Title == "Lô thanh lý kết thúc chưa bán được").ToListAsync());
    }

    [Fact]
    public async Task Concurrent_payment_confirmations_record_one_timestamp()
    {
        Guid agreementId;
        await using (var db = Open()) { (_, agreementId) = await AwardedLot(db); await SettleDeposit(db, agreementId); await Depot(db).AgreementActionAsync(owner, depot, agreementId, "REPORT_PAYMENT", "CK-002"); }
        async Task<DateTime?> Confirm() { await using var db = Open(); return (await Seller(db).ConfirmPaymentAsync(seller, agreementId)).PaymentConfirmedAt; }
        var results = await Task.WhenAll(Confirm(), Confirm(), Confirm());
        Assert.Single(results.Distinct());
        await using var check = Open();
        Assert.Single(await check.Notifications.Where(x => x.UserId == owner && x.Title == "Seller đã xác nhận nhận tiền").ToListAsync());
    }
}
