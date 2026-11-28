using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Liquidation;
using Xunit;

namespace Retrack.API.Tests;

/// <summary>
/// Phiên nhiều hạng mục trên PostgreSQL thật (schema tạm trong Retrack_TV2_test, tự xóa sau test).
/// Kiểm tra: hai kho thắng hai hạng mục, tiền theo lượng đã đăng, retry/idempotency, gia hạn, race, quyền, nhập kho TV2, bỏ mua/Admin.
/// </summary>
public sealed class LiquidationV2PostgresTests : IAsyncLifetime
{
    private readonly string schema = "liq_v2_" + Guid.NewGuid().ToString("N");
    private NpgsqlConnection admin = null!;
    private string connection = null!;
    private readonly Guid seller = Guid.NewGuid(), ownerA = Guid.NewGuid(), ownerB = Guid.NewGuid(), adminUser = Guid.NewGuid();
    private readonly Guid depotA = Guid.NewGuid(), depotB = Guid.NewGuid();
    private readonly ManualClock clock = new(DateTime.UtcNow);

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(source)) return;
        var parsed = new NpgsqlConnectionStringBuilder(source);
        Assert.Equal("Retrack_TV2_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
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
            new User { Id = seller, Role = "SELLER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Seller thử", Phone = "0900000001" },
            new User { Id = ownerA, Role = "DEPOT_OWNER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Chủ kho A", Phone = "0900000002" },
            new User { Id = ownerB, Role = "DEPOT_OWNER", Email = Guid.NewGuid() + "@test.invalid", FullName = "Chủ kho B", Phone = "0900000003" },
            new User { Id = adminUser, Role = "ADMIN", Email = Guid.NewGuid() + "@test.invalid", FullName = "Admin thử" });
        db.Depots.AddRange(new Depot { Id = depotA, OwnerId = ownerA, Name = "Kho A", VerificationStatus = "APPROVED" },
            new Depot { Id = depotB, OwnerId = ownerB, Name = "Kho B", VerificationStatus = "APPROVED" });
        db.DepotSubscriptions.AddRange(new DepotSubscription { DepotId = depotA, TrialStartedAt = clock.Now, TrialEndsAt = clock.Now.AddDays(14) },
            new DepotSubscription { DepotId = depotB, TrialStartedAt = clock.Now, TrialEndsAt = clock.Now.AddDays(14) });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (admin == null) return;
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await drop.ExecuteNonQueryAsync(); await admin.DisposeAsync();
    }

    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private LiquidationV2Service Service(AppDbContext db) => new(new LiquidationV2Repository(db),
        new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db),
            new DepotSubscriptionService(new DepotSubscriptionRepository(db), new NoPayOs(), clock)), new DepotUnitOfWork(db), new DepotBatchRepository(db), clock);
    private async Task<T> Run<T>(Func<LiquidationV2Service, Task<T>> action) { await using var db = Open(); return await action(Service(db)); }
    private async Task Run(Func<LiquidationV2Service, Task> action) { await using var db = Open(); await action(Service(db)); }

    private CreateLiquidationV2Dto Campaign(Guid operation)
    {
        var t = clock.Now;
        return new()
        {
            OperationId = operation, Title = "Thanh lý xưởng cơ khí", Description = "Thanh lý sắt vụn và tivi cũ tại xưởng.",
            CollectionTerms = "Bên mua tự bốc xếp, thu gom trong giờ hành chính.", PublicArea = "Hải Châu, Đà Nẵng",
            Address = "12 Đường Thử, Hải Châu", ContactPhone = "0900000001",
            SurveyStart = t.AddMinutes(1), SurveyEnd = t.AddMinutes(10), BiddingStart = t.AddMinutes(10), BiddingEnd = t.AddMinutes(60),
            CollectionWindowHours = 72,
            Items =
            [
                new() { MaterialCode = "IRON", Name = "Sắt vụn", Description = "Sắt lẫn tạp, cần phân loại", Unit = "kg", Quantity = 100, StartingUnitPrice = 3000, BidIncrement = 100 },
                new() { Name = "Tivi CRT cũ", Description = "Tivi CRT 21 inch, không còn chạy", Unit = "cai", Quantity = 2, StartingUnitPrice = 50000, BidIncrement = 5000 },
            ]
        };
    }

    private async Task<(Guid Lot, Guid Iron, Guid Tv)> OpenCampaignWithBothDepotsAsync(bool legacy = true)
    {
        var created = await Run(s => s.CreateAsync(seller, Campaign(Guid.NewGuid())));
        // Các ca cũ kiểm tương thích điều kiện đã công bố trước khi có chốt cân.
        if (legacy) { await using var db=Open();var old=await db.LiquidationLots.SingleAsync(x=>x.Id==created.Campaign.Id);old.PricingBasis="POSTED_QUANTITY";await db.SaveChangesAsync(); }
        var iron = created.Items.Single(x => x.Unit == "kg").Id; var tv = created.Items.Single(x => x.Unit == "cai").Id;
        clock.Advance(TimeSpan.FromMinutes(2));
        await Run(s => s.RegisterAsync(ownerA, depotA, created.Campaign.Id, new(true, [iron])));
        await Run(s => s.RegisterAsync(ownerB, depotB, created.Campaign.Id, new(true, [iron, tv])));
        await Run(s => s.ConfirmSurveyAsync(ownerA, depotA, created.Campaign.Id, true));
        await Run(s => s.ConfirmSurveyAsync(ownerB, depotB, created.Campaign.Id, true));
        clock.Advance(TimeSpan.FromMinutes(9)); // vào giờ trả giá
        return (created.Campaign.Id, iron, tv);
    }


    private async Task<LiquidationV2AgreementDto> AwardNewAgreement()
    {
        var (lot,iron,tv)=await OpenCampaignWithBothDepotsAsync(legacy:false);
        await Run(s=>s.BidAsync(ownerB,depotB,lot,iron,new(Guid.NewGuid(),3000,0)));
        await Run(s=>s.BidAsync(ownerB,depotB,lot,tv,new(Guid.NewGuid(),50000,0)));
        clock.Advance(TimeSpan.FromHours(2));await Run(s=>s.FinalizeAsync(lot));
        var details=await Run(s=>s.DetailAsync(ownerB,depotB,lot));
        return await Run(s=>s.AgreementAsync(ownerB,depotB,details.AgreementId!.Value));
    }
    private static ProposeLiquidationHandoverDto Proposal(LiquidationV2AgreementDto a,decimal iron=95,int revision=0) => new()
    {
        OperationId=Guid.NewGuid(),ExpectedRevision=revision,
        Lines=a.Items.Select(x=>new LiquidationHandoverLineInput(x.Id,x.Unit=="kg"?iron:2)).ToList()
    };

    [PostgresV2Fact]
    public async Task Confirmed_handover_locks_exact_item_amounts_and_preserves_auction_snapshot()
    {
        var a=await AwardNewAgreement();Assert.Equal("WAITING_BUYER_QUANTITY",a.Stage);
        Assert.Equal(LiquidationV2Service.ConfirmedQuantityPricing,a.PricingBasis);
        await Assert.ThrowsAsync<DepotConflictException>(()=>Run(s=>s.SellerActionAsync(seller,a.Id,"CONFIRM_PAYMENT")));
        var input=Proposal(a);
        var proposed=await Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,input));
        Assert.Equal("WAITING_SELLER_QUANTITY",proposed.Stage);Assert.Equal(385000,proposed.Handover!.Amount);
        var retry=await Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,input));
        Assert.Equal(proposed.Handover.Id,retry.Handover!.Id);
        await Assert.ThrowsAsync<DepotForbiddenException>(()=>Run(s=>s.ProposeHandoverAsync(ownerA,depotB,a.Id,input)));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Run(s=>s.DecideHandoverAsync(ownerA,a.Id,new(1,true,null))));
        var accepted=await Run(s=>s.DecideHandoverAsync(seller,a.Id,new(1,true,null)));
        Assert.Equal("WAITING_BUYER_PAYMENT",accepted.Stage);Assert.Equal(385000,accepted.TotalAmount);
        Assert.Equal(100,accepted.Items.Single(x=>x.Unit=="kg").Quantity);
        Assert.Equal(300000,accepted.Items.Single(x=>x.Unit=="kg").Amount);
        await Run(s=>s.DecideHandoverAsync(seller,a.Id,new(1,true,null)));
        await Assert.ThrowsAsync<DepotConflictException>(()=>Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,Proposal(a,90,1))));
        await Run(s=>s.BuyerActionAsync(ownerB,depotB,a.Id,"REPORT_PAYMENT","CK đã trả 385000"));
        await Run(s=>s.SellerActionAsync(seller,a.Id,"CONFIRM_PAYMENT"));
        await Run(s=>s.SellerActionAsync(seller,a.Id,"CONFIRM_HANDOVER"));
        var source=await Run(s=>s.IntakeSourceAsync(ownerB,depotB,a.Id));
        Assert.True(source.ReadyForIntake);Assert.Equal(3,source.FormatVersion);
        Assert.Equal(95,source.Items.Single(x=>x.Unit=="kg").ConfirmedQuantity);
        Assert.Equal(285000,source.Items.Single(x=>x.Unit=="kg").FinalAmount);
        await using var db=Open();
        Assert.Single(await db.LiquidationHandovers.Where(x=>x.AgreementId==a.Id).ToListAsync());
        Assert.Equal(1,await db.Notifications.CountAsync(x=>x.UserId==ownerB && x.Title=="Seller đã xác nhận số cân/kiểm đếm"));
    }
    [PostgresV2Fact]
    public async Task Rejected_or_superseded_handover_keeps_history_and_rejects_stale_confirmation()
    {
        var a=await AwardNewAgreement();var first=Proposal(a);
        await Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,first));
        var rejected=await Run(s=>s.DecideHandoverAsync(seller,a.Id,new(1,false,"Cần cân lại trước khi trả tiền")));
        Assert.Equal("WAITING_BUYER_QUANTITY",rejected.Stage);
        await Assert.ThrowsAsync<DepotConflictException>(()=>Run(s=>s.BuyerActionAsync(ownerB,depotB,a.Id,"REPORT_PAYMENT","Chưa chốt")));
        await Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,Proposal(a,90,1)));
        await Assert.ThrowsAsync<DepotConflictException>(()=>Run(s=>s.DecideHandoverAsync(seller,a.Id,new(1,true,null))));
        var accepted=await Run(s=>s.DecideHandoverAsync(seller,a.Id,new(2,true,null)));
        Assert.Equal(370000,accepted.TotalAmount);
        await using var db=Open();
        var rows=await db.LiquidationHandovers.Where(x=>x.AgreementId==a.Id).OrderBy(x=>x.Revision).ToListAsync();
        Assert.Equal(new[]{"REJECTED","CONFIRMED"},rows.Select(x=>x.Status));
        Assert.Equal("Cần cân lại trước khi trả tiền",rows[0].DecisionNote);
    }
    [PostgresV2Fact]
    public async Task Concurrent_proposals_and_seller_decision_cannot_confirm_stale_quantities()
    {
        var a=await AwardNewAgreement();
        async Task<bool> Attempt(Func<Task> action){try{await action();return true;}catch(DepotConflictException){return false;}}
        var proposalResults=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Attempt(()=>Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,Proposal(a))))));
        Assert.Equal(1,proposalResults.Count(x=>x));
        var decisionResults=await Task.WhenAll(
            Attempt(()=>Run(s=>s.DecideHandoverAsync(seller,a.Id,new(1,true,null)))),
            Attempt(()=>Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,Proposal(a,90,1)))));
        Assert.Equal(1,decisionResults.Count(x=>x));
        var current=await Run(s=>s.AgreementAsync(ownerB,depotB,a.Id));
        if(current.HandoverStatus=="CONFIRMED"){Assert.Equal(1,current.HandoverRevision);Assert.Equal(385000,current.TotalAmount);}
        else {Assert.Equal(2,current.HandoverRevision);Assert.Equal("PENDING",current.HandoverStatus);}
    }
    [PostgresV2Fact]
    public async Task Handover_validates_units_membership_operation_payload_and_rolls_back_invalid_rows()
    {
        var a=await AwardNewAgreement();
        foreach(var invalid in new[]{0m,-1m,1.1111m})
            await Assert.ThrowsAsync<ArgumentException>(()=>Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,Proposal(a,invalid))));
        var badCount=Proposal(a);
        // Không phụ thuộc thứ tự hạng mục khi chọn dòng cái/bộ.
        var piece=a.Items.Single(x=>x.Unit!="kg").Id;
        badCount.Lines=Proposal(a).Lines.Select(x=>x.AgreementItemId==piece?x with {Quantity=1.5m}:x).ToList();
        await Assert.ThrowsAsync<ArgumentException>(()=>Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,badCount)));
        var missing=Proposal(a);missing.Lines.RemoveAt(0);
        await Assert.ThrowsAsync<ArgumentException>(()=>Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,missing)));
        var source=Proposal(a);await Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,source));
        source.Note="Nội dung khác";
        await Assert.ThrowsAsync<DepotConflictException>(()=>Run(s=>s.ProposeHandoverAsync(ownerB,depotB,a.Id,source)));
        await using var db=Open();Assert.Equal(1,await db.LiquidationHandovers.CountAsync(x=>x.AgreementId==a.Id));
        Assert.Equal(1,(await db.LiquidationAgreements.SingleAsync(x=>x.Id==a.Id)).HandoverRevision);
    }

    [PostgresV2Fact]
    public async Task New_campaign_blocks_payment_until_seller_confirms_handover_quantity()
    {
        var (lot,iron,_) = await OpenCampaignWithBothDepotsAsync(legacy:false);
        await Run(s=>s.BidAsync(ownerA,depotA,lot,iron,new(Guid.NewGuid(),3000,0)));
        clock.Advance(TimeSpan.FromHours(2));
        await Run(s=>s.FinalizeAsync(lot));
        var agreement=(await Run(s=>s.DetailAsync(ownerA,depotA,lot))).AgreementId!.Value;
        await Assert.ThrowsAsync<DepotConflictException>(()=>Run(s=>s.BuyerActionAsync(ownerA,depotA,agreement,"REPORT_PAYMENT","Chưa chốt cân")));
    }

    [PostgresV2Fact]
    public async Task Two_depots_win_different_items_and_pay_by_posted_quantity()
    {
        var (lot, iron, tv) = await OpenCampaignWithBothDepotsAsync();
        // Kho A chưa đăng ký tivi nên không được trả giá hạng mục này.
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Run(s => s.BidAsync(ownerA, depotA, lot, tv, new(Guid.NewGuid(), 50000, null))));
        await Run(s => s.BidAsync(ownerB, depotB, lot, iron, new(Guid.NewGuid(), 3000, 0)));
        var opA = Guid.NewGuid();
        var first = await Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(opA, 3200, 1)));
        // Gửi lại cùng thao tác: trả kết quả cũ, không tạo lượt mới.
        var retry = await Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(opA, 3200, 1)));
        Assert.Equal(first.Id, retry.Id);
        // Revision cũ (ExpectedBidCount) bị từ chối với dữ liệu giá mới.
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.BidAsync(ownerB, depotB, lot, iron, new(Guid.NewGuid(), 3300, 1))));
        await Run(s => s.BidAsync(ownerB, depotB, lot, tv, new(Guid.NewGuid(), 55000, 0)));

        var viewB = await Run(s => s.DetailAsync(ownerB, depotB, lot));
        var ironB = viewB.Items.Single(x => x.Id == iron);
        Assert.Equal(3200, ironB.HighestUnitPrice); Assert.False(ironB.Leading); Assert.Equal(3300, ironB.NextMinUnitPrice);
        Assert.True(viewB.Items.Single(x => x.Id == tv).Leading);

        clock.Advance(TimeSpan.FromMinutes(60));
        await Run(s => s.FinalizeAsync(lot));
        await Run(s => s.FinalizeAsync(lot)); // đóng lặp không sinh thỏa thuận trùng

        await using var check = Open();
        var agreements = await check.LiquidationAgreements.Include(x => x.Items).Where(x => x.LotId == lot).ToListAsync();
        Assert.Equal(2, agreements.Count);
        var a = agreements.Single(x => x.DepotId == depotA);
        Assert.Equal(320_000, a.Amount); // 100 kg x 3.200 đ — theo lượng Seller đã đăng
        Assert.Equal(2, a.FormatVersion); Assert.Null(a.WinningBidId); Assert.Equal(0, a.DepositAmount);
        var b = agreements.Single(x => x.DepotId == depotB);
        Assert.Equal(110_000, b.Amount); // 2 cái x 55.000 đ
        Assert.Equal("AWARDED", (await check.LiquidationLots.SingleAsync(x => x.Id == lot)).Status);

        // Kho thua hạng mục không đọc được thỏa thuận của kho khác.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(s => s.AgreementAsync(ownerB, depotB, a.Id)));
        // V1 không thấy phiên/thỏa thuận V2.
        var v1 = await check.LiquidationLots.CountAsync(x => x.FormatVersion == 1);
        Assert.Equal(0, v1);
    }

    [PostgresV2Fact]
    public async Task Late_bids_extend_shared_end_two_minutes_at_most_five_times()
    {
        var (lot, iron, _) = await OpenCampaignWithBothDepotsAsync();
        await using (var db = Open()) clock.Set((await db.LiquidationLots.SingleAsync(x => x.Id == lot)).BiddingEnd.AddSeconds(-30));
        decimal price = 3000;
        for (var i = 0; i < 6; i++)
        {
            var who = i % 2 == 0 ? (ownerA, depotA) : (ownerB, depotB);
            var result = await Run(s => s.BidAsync(who.Item1, who.Item2, lot, iron, new(Guid.NewGuid(), price, null)));
            Assert.Equal(i < 5, result.Extended);
            Assert.Equal(Math.Min(i + 1, 5), result.ExtensionCount);
            clock.Set(result.BiddingEnd.AddSeconds(-30));
            price += 100;
        }
        await using var check = Open();
        var saved = await check.LiquidationLots.SingleAsync(x => x.Id == lot);
        Assert.Equal(saved.OriginalEnd.AddMinutes(10), saved.BiddingEnd);
    }

    [PostgresV2Fact]
    public async Task Concurrent_equal_bids_produce_a_single_leader()
    {
        var (lot, iron, _) = await OpenCampaignWithBothDepotsAsync();
        var tasks = new[] { (ownerA, depotA), (ownerB, depotB) }.Select(x => Task.Run(async () =>
        {
            try { await Run(s => s.BidAsync(x.Item1, x.Item2, lot, iron, new(Guid.NewGuid(), 3000, null))); return true; }
            catch (DepotConflictException) { return false; }
        })).ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(x => x));
        await using var check = Open();
        Assert.Equal(1, await check.LiquidationItemBids.CountAsync(x => x.ItemId == iron));
        Assert.Equal(1, (await check.LiquidationItems.SingleAsync(x => x.Id == iron)).BidCount);
    }

    [PostgresV2Fact]
    public async Task Agreement_flow_feeds_existing_stock_intake_once()
    {
        var (lot, iron, _) = await OpenCampaignWithBothDepotsAsync();
        await Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(Guid.NewGuid(), 3000, null)));
        clock.Advance(TimeSpan.FromMinutes(60));
        await Run(s => s.FinalizeAsync(lot));
        Guid agreementId; await using (var db = Open()) agreementId = (await db.LiquidationAgreements.SingleAsync(x => x.LotId == lot)).Id;

        var source = await Run(s => s.IntakeSourceAsync(ownerA, depotA, agreementId));
        Assert.False(source.ReadyForIntake); Assert.Equal(300_000, source.FinalPurchaseAmount);
        Assert.Equal("POSTED_QUANTITY", source.PricingBasis); Assert.Equal(100, Assert.Single(source.Items).PostedQuantity);

        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.SellerActionAsync(seller, agreementId, "CONFIRM_PAYMENT")));
        await Run(s => s.BuyerActionAsync(ownerA, depotA, agreementId, "REPORT_PAYMENT", "Chuyển khoản VCB 123"));
        await Run(s => s.SellerActionAsync(seller, agreementId, "CONFIRM_PAYMENT"));
        await Run(s => s.SellerActionAsync(seller, agreementId, "CONFIRM_HANDOVER"));
        // Sau bàn giao không còn báo bỏ mua.
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.BuyerActionAsync(ownerA, depotA, agreementId, "REPORT_ABANDON", "Đổi ý không mua")));
        Assert.True((await Run(s => s.IntakeSourceAsync(ownerA, depotA, agreementId))).ReadyForIntake);

        // TV2: nhập 90 kg thực tế sau phân loại + chi phí 45.000 đ; giá mua giữ 300.000 đ theo thỏa thuận.
        var input = new CreateStockReceiptDto { OperationId = Guid.NewGuid(), BulkAgreementId = agreementId, SourceType = "BULK_PURCHASE",
            Reference = "Nhập sắt phiên V2", ReceivedAt = DateTime.UtcNow, PurchaseCost = 300_000, CostsConfirmed = true, PostImmediately = true, ConfirmBulkReceived = true,
            Lines = [new() { Kind = "MATERIAL", MaterialType = "IRON", Name = "Sắt đã phân loại", Unit = "kg", Quantity = 90 }],
            Expenses = [new("LABOR", "Phân loại", 45_000)] };
        for (var i = 0; i < 2; i++)
        {
            await using var db = Open();
            await new DepotStockService(new DepotStockRepository(db), new DepotBatchRepository(db), new DepotUnitOfWork(db),
                new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db))).CreateAsync(ownerA, depotA, input);
        }
        await using var check = Open();
        var receipt = await check.DepotStockReceipts.SingleAsync(x => x.BulkAgreementId == agreementId);
        Assert.Equal(300_000, receipt.PurchaseCost);
        var line = await check.DepotStockLines.SingleAsync(x => x.ReceiptId == receipt.Id);
        Assert.Equal(90, line.Quantity); Assert.Equal(345_000, line.AllocatedCost);
        Assert.NotNull((await check.LiquidationAgreements.SingleAsync(x => x.Id == agreementId)).OwnerCollectedAt);
        var after = await Run(s => s.AgreementAsync(ownerA, depotA, agreementId));
        Assert.Equal("COMPLETED", after.Stage); Assert.Equal(receipt.Id, after.ReceiptId); Assert.Null(after.NextAction);
    }

    [PostgresV2Fact]
    public async Task Abandon_report_pauses_flow_and_admin_cancel_blocks_without_reassigning()
    {
        var (lot, iron, _) = await OpenCampaignWithBothDepotsAsync();
        await Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(Guid.NewGuid(), 3000, null)));
        await Run(s => s.BidAsync(ownerB, depotB, lot, iron, new(Guid.NewGuid(), 3100, null)));
        clock.Advance(TimeSpan.FromMinutes(60));
        await Run(s => s.FinalizeAsync(lot));
        Guid agreementId; await using (var db = Open()) agreementId = (await db.LiquidationAgreements.SingleAsync(x => x.LotId == lot)).Id;

        var reported = await Run(s => s.SellerActionAsync(seller, agreementId, "REPORT_ABANDON", "Bên mua không đến nhận và không nghe máy"));
        Assert.Equal("DISPUTED", reported.Stage); Assert.Equal("SELLER", reported.AbandonReporterRole);
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.BuyerActionAsync(ownerB, depotB, agreementId, "REPORT_PAYMENT", "CK")));
        var open = await Run(s => s.DisputesAsync(true, 1, 20));
        Assert.Equal(agreementId, Assert.Single(open.Items).AgreementId);

        await Assert.ThrowsAsync<ArgumentException>(() => Run(s => s.ResolveAsync(adminUser, agreementId, new("PENALTY", "Phạt cọc"))));
        var resolved = await Run(s => s.ResolveAsync(adminUser, agreementId, new("CANCELLED", "Xác minh bên mua bỏ mua")));
        Assert.Equal("CANCELLED", resolved.Stage);
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.SellerActionAsync(seller, agreementId, "CONFIRM_PAYMENT")));
        Assert.False((await Run(s => s.IntakeSourceAsync(ownerB, depotB, agreementId))).ReadyForIntake);
        await using var check = Open();
        // Không tự chuyển người thắng sang người trả giá thứ hai.
        Assert.Equal(1, await check.LiquidationAgreements.CountAsync(x => x.LotId == lot));
        Assert.Empty(await Run(s => s.DisputesAsync(true, 1, 20)).ContinueWith(t => t.Result.Items));
    }

    [PostgresV2Fact]
    public async Task Invalid_campaigns_and_late_registration_are_rejected()
    {
        var bad = Campaign(Guid.NewGuid());
        bad.Items[1].Quantity = 1.5m; // cái phải là số nguyên
        await Assert.ThrowsAsync<ArgumentException>(() => Run(s => s.CreateAsync(seller, bad)));
        var tooMany = Campaign(Guid.NewGuid());
        tooMany.Items = Enumerable.Range(1, 21).Select(i => new LiquidationItemInput { Name = $"Hạng mục {i}", Description = "Mô tả vật liệu", Unit = "kg", Quantity = 1, StartingUnitPrice = 1000, BidIncrement = 100 }).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => Run(s => s.CreateAsync(seller, tooMany)));

        var op = Guid.NewGuid();
        var created = await Run(s => s.CreateAsync(seller, Campaign(op)));
        Assert.Equal(created.Campaign.Id, (await Run(s => s.CreateAsync(seller, Campaign(op)))).Campaign.Id); // retry cùng nội dung
        var changed = Campaign(op); changed.Title = "Tiêu đề khác hẳn";
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.CreateAsync(seller, changed)));

        clock.Advance(TimeSpan.FromMinutes(11)); // hết hạn đăng ký khảo sát
        await Assert.ThrowsAsync<DepotConflictException>(() => Run(s => s.RegisterAsync(ownerA, depotA, created.Campaign.Id, new(true, [created.Items[0].Id]))));
    }
    [PostgresV2Fact]
    public async Task Expired_subscription_blocks_new_registration_and_does_not_create_participation()
    {
        var created = await Run(s => s.CreateAsync(seller, Campaign(Guid.NewGuid())));
        clock.Advance(TimeSpan.FromMinutes(2));
        await ExpireAsync();
        await Assert.ThrowsAsync<DepotSubscriptionRequiredException>(() => Run(s => s.RegisterAsync(ownerA, depotA, created.Campaign.Id,
            new(true, [created.Items[0].Id]))));
        await using var check = Open();
        Assert.Empty(await check.LiquidationParticipations.Where(x => x.DepotId == depotA).ToListAsync());
    }

    [PostgresV2Fact]
    public async Task Expired_subscription_blocks_new_bid_but_preserves_retry_and_existing_agreement_intake()
    {
        var (lot, iron, _) = await OpenCampaignWithBothDepotsAsync();
        var operation = Guid.NewGuid();
        var bid = await Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(operation, 3000, null)));
        await ExpireAsync();
        var retry = await Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(operation, 3000, null)));
        Assert.Equal(bid.Id, retry.Id);
        await Assert.ThrowsAsync<DepotSubscriptionRequiredException>(() => Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(Guid.NewGuid(), 3100, null))));
        clock.Advance(TimeSpan.FromMinutes(60)); await Run(s => s.FinalizeAsync(lot));
        Guid agreement; await using (var db = Open()) agreement = (await db.LiquidationAgreements.SingleAsync(x => x.LotId == lot)).Id;
        await Run(s => s.BuyerActionAsync(ownerA, depotA, agreement, "REPORT_PAYMENT", "CK DEMO hợp lệ"));
        await Run(s => s.SellerActionAsync(seller, agreement, "CONFIRM_PAYMENT"));
        await Run(s => s.SellerActionAsync(seller, agreement, "CONFIRM_HANDOVER"));
        await using var post = Open();
        var sub = new DepotSubscriptionService(new DepotSubscriptionRepository(post), new NoPayOs(), clock);
        var receipt = await new DepotStockService(new DepotStockRepository(post), new DepotBatchRepository(post), new DepotUnitOfWork(post),
            new DepotService(new DepotOwnerRepository(post), new DepotPaymentReadRepository(post)), subscriptions: sub)
            .CreateAsync(ownerA, depotA, Intake(agreement));
        Assert.Equal("POSTED", receipt.Status);
    }

    [PostgresV2Theory, InlineData(true), InlineData(false)]
    public async Task Stock_intake_rechecks_cancelled_or_disputed_source_even_with_handover_flags(bool cancelled)
    {
        var (lot, iron, _) = await OpenCampaignWithBothDepotsAsync();
        await Run(s => s.BidAsync(ownerA, depotA, lot, iron, new(Guid.NewGuid(), 3000, null)));
        clock.Advance(TimeSpan.FromMinutes(60)); await Run(s => s.FinalizeAsync(lot));
        Guid id;
        await using (var db = Open())
        {
            var agreement = await db.LiquidationAgreements.SingleAsync(x => x.LotId == lot); id = agreement.Id;
            // Cố ý tạo trạng thái bất nhất trong fixture để chứng minh API kho tự phòng vệ, không dựa vào UI.
            agreement.PaymentReportedAt = clock.Now; agreement.PaymentConfirmedAt = clock.Now; agreement.SellerHandedOverAt = clock.Now;
            agreement.AbandonReportedAt = clock.Now; agreement.AbandonReportedBy = seller; agreement.AbandonReason = "Fixture kiểm tra nguồn không còn hợp lệ";
            agreement.Resolution = cancelled ? "CANCELLED" : null;
            if (cancelled) { agreement.ResolvedAt = clock.Now; agreement.ResolvedBy = adminUser; agreement.ResolutionNote = "Admin hủy trong fixture"; }
            await db.SaveChangesAsync();
        }
        await using var post = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => new DepotStockService(new DepotStockRepository(post), new DepotBatchRepository(post), new DepotUnitOfWork(post),
            new DepotService(new DepotOwnerRepository(post), new DepotPaymentReadRepository(post))).CreateAsync(ownerA, depotA, Intake(id)));
        Assert.False(await post.DepotStockReceipts.AnyAsync(x => x.BulkAgreementId == id));
        await using var check = Open(); Assert.Null((await check.LiquidationAgreements.SingleAsync(x => x.Id == id)).OwnerCollectedAt);
    }

    private async Task ExpireAsync()
    {
        await using var db = Open(); var sub = await db.DepotSubscriptions.SingleAsync(x => x.DepotId == depotA);
        sub.TrialEndsAt = clock.Now.AddMinutes(-1); sub.PaidUntil = null; await db.SaveChangesAsync();
    }
    private static CreateStockReceiptDto Intake(Guid agreement) => new()
    {
        OperationId = Guid.NewGuid(), BulkAgreementId = agreement, SourceType = "BULK_PURCHASE", Reference = "DEMO nhập sau thắng V2",
        ReceivedAt = DateTime.UtcNow, PurchaseCost = 300_000, CostsConfirmed = true, PostImmediately = true, ConfirmBulkReceived = true,
        Lines = [new() { Kind = "MATERIAL", MaterialType = "IRON", Name = "Sắt", Unit = "kg", Quantity = 90 }], Expenses = [new("OTHER", "Vận chuyển và phân loại", 45_000)]
    };
    private sealed class NoPayOs : IPayOsClient
    {
        public bool IsConfigured => false;
        public Task<PayOsLink> CreatePaymentLinkAsync(long code, long amount, string description, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PayOsPayment> GetPaymentAsync(long code, CancellationToken ct = default) => throw new NotSupportedException();
        public bool VerifyWebhookData(System.Text.Json.JsonElement data, string signature) => false;
    }

}

public sealed class ManualClock(DateTime start) : TimeProvider
{
    private DateTime now = DateTime.SpecifyKind(start, DateTimeKind.Utc);
    public DateTime Now => now;
    public override DateTimeOffset GetUtcNow() => new(now);
    public void Advance(TimeSpan by) => now = now.Add(by);
    public void Set(DateTime value) => now = DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public sealed class PostgresV2FactAttribute : FactAttribute
{
    public PostgresV2FactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL test riêng (Retrack_TV2_test) để kiểm tra phiên nhiều hạng mục.";
    }
}

public sealed class PostgresV2TheoryAttribute : TheoryAttribute
{
    public PostgresV2TheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL test riêng (Retrack_TV2_test) để kiểm tra nguồn nhập.";
    }
}
