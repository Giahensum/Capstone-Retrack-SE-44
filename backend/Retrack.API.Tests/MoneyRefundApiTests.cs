using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Models;
using Xunit;

namespace Retrack.API.Tests;

/// <summary>API Admin duyệt/từ chối ngân hàng, bất thường thu tiền và hoàn tiền; API người trả xem hoàn tiền của mình.</summary>
public sealed partial class PayoutApiTests
{
    private const string NewAccountBody = "123456789";

    private object BankBody(string account = NewAccountBody, string holder = "NGUYEN VAN A") =>
        new { bankName = "ACB", bankBin = "970416", accountNumber = account, accountName = holder };

    private async Task<Guid> NewCollectionFor(Guid payer, Guid payee, string flow = "PICKUP", decimal amount = 1_000_000, string status = "PAID")
    {
        await using var db = Open();
        var sourceType = flow == "BATCH" ? MoneySources.InventoryBatch : flow.StartsWith("LIQUIDATION") ? MoneySources.LiquidationAgreement : MoneySources.PickupRequest;
        var c = new MoneyCollection { Flow = flow, SourceType = sourceType, SourceId = Guid.NewGuid(), PayerId = payer, PayeeId = payee, Amount = amount, Status = status, ReceivedAmount = amount, PaidAt = T0, CreatedAt = T0 };
        db.MoneyCollections.Add(c); await db.SaveChangesAsync(); return c.Id;
    }

    private async Task<Guid> NewAnomalyFor(Guid collection, string kind = "OVERPAID", decimal refund = 50_000, string status = "OPEN", DateTime? at = null)
    {
        await using var db = Open();
        var a = new MoneyCollectionAnomaly { CollectionId = collection, OrderCode = 1, Kind = kind, ReceivedAmount = refund, ExpectedAmount = 1_000_000, RefundAmount = refund,
            Reference = "REF-" + Guid.NewGuid().ToString("N")[..10], Status = status, CreatedAt = at ?? T0 };
        db.MoneyCollectionAnomalies.Add(a); await db.SaveChangesAsync(); return a.Id;
    }

    private static JsonElement Data(JsonElement body) => body.GetProperty("data");

    // ============================ NGÂN HÀNG: DUYỆT / TỪ CHỐI ============================

    [PostgresMoneyFact]
    public async Task AdminRejectsBankWithReasonAndBadgeFilterAndNotificationWork()
    {
        var u = await NewPayee("Người A", bank: false);
        var b = await NewPayee("Người B", bank: false);
        await AddLine(b, 100_000, 0);
        Assert.Equal(HttpStatusCode.OK, (await Call("PUT", "/api/money/my/bank", "SELLER", u, BankBody())).Status);
        Assert.Equal(1, Data((await AsAdmin("GET", "/api/admin/money/payees/pending-bank-count")).Body).GetProperty("pendingBankCount").GetInt32());

        Assert.Equal(1, Data((await AsAdmin("GET", "/api/admin/money/payees?bankStatus=PENDING_VERIFICATION")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, Data((await AsAdmin("GET", "/api/admin/money/payees?bankStatus=VERIFIED")).Body).GetProperty("totalCount").GetInt32());
        var none = await AsAdmin("GET", "/api/admin/money/payees?bankStatus=none&search=Ng%C6%B0%E1%BB%9Di%20B");
        Assert.Equal(1, Data(none.Body).GetProperty("totalCount").GetInt32());
        Assert.Equal("NONE", Data(none.Body).GetProperty("items")[0].GetProperty("bank").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("GET", "/api/admin/money/payees?bankStatus=BOGUS")).Status);

        var reject = $"/api/admin/money/payees/{u}/bank/reject";
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", reject, new { expectedAccountNumber = NewAccountBody, reason = "abc" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", reject, new { expectedAccountNumber = NewAccountBody, reason = new string('x', 256) })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", reject, new { expectedAccountNumber = "999999999", reason = "Số tài khoản không khớp" })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", reject, new { expectedAccountNumber = "", reason = "Số tài khoản không khớp" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("POST", reject, "SELLER", u, new { expectedAccountNumber = NewAccountBody, reason = "Số tài khoản không khớp" })).Status);
        const string reason = "Tên chủ tài khoản không khớp CCCD";
        var ok = await AsAdmin("POST", reject, new { expectedAccountNumber = NewAccountBody, reason });
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal("REJECTED", Data(ok.Body).GetProperty("status").GetString());
        Assert.Equal(reason, Data(ok.Body).GetProperty("rejectReason").GetString());
        Assert.Equal(JsonValueKind.Null, Data(ok.Body).GetProperty("verifiedAt").ValueKind);

        var mine = Data((await Call("GET", "/api/money/my/bank", "SELLER", u)).Body);
        Assert.Equal("REJECTED", mine.GetProperty("status").GetString());
        Assert.Equal(reason, mine.GetProperty("rejectReason").GetString());
        var detail = Data((await AsAdmin("GET", $"/api/admin/money/payees/{u}/bank")).Body);
        Assert.Equal("REJECTED", detail.GetProperty("status").GetString());
        Assert.Equal(reason, detail.GetProperty("rejectReason").GetString());
        Assert.False(detail.GetProperty("nameMatchesFullName").GetBoolean()); // chỉ là gợi ý
        await using (var db = Open())
        {
            Assert.True(await db.Notifications.AnyAsync(n => n.UserId == u && n.Title.Contains("bị từ chối") && n.Message!.Contains(reason)));
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "PAYEE_BANK_REJECTED" && a.UserId == AdminUser && a.EntityId == u));
        }
        Assert.Equal(0, Data((await AsAdmin("GET", "/api/admin/money/payees/pending-bank-count")).Body).GetProperty("pendingBankCount").GetInt32());
        Assert.Equal(1, Data((await AsAdmin("GET", "/api/admin/money/payees?bankStatus=REJECTED")).Body).GetProperty("totalCount").GetInt32());

        // tài khoản bị từ chối thì kỳ chi bị giữ
        await AddLine(u, 200_000, 0);
        var plan = await RunOne(u);
        Assert.Equal("HELD", plan.Status);
        Assert.Contains("từ chối", plan.Reason);
        Assert.Equal(0, Fake.PayoutCalls);

        // nhập lại: về chờ duyệt, lý do cũ biến mất
        var again = await Call("PUT", "/api/money/my/bank", "SELLER", u, BankBody("555666777"));
        Assert.Equal("PENDING_VERIFICATION", Data(again.Body).GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, Data(again.Body).GetProperty("rejectReason").ValueKind);
        // Admin xem số cũ rồi mới từ chối: 409
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", reject, new { expectedAccountNumber = NewAccountBody, reason = "Số tài khoản không khớp" })).Status);
        // người dùng chưa nhập ngân hàng: không từ chối được (400)
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", $"/api/admin/money/payees/{AdminUser}/bank/reject", new { expectedAccountNumber = "1", reason = "Số tài khoản không khớp" })).Status);
    }

    [PostgresMoneyFact]
    public async Task VerifyReleasesHeldRunsAutomaticallyAndLaterChangeHoldsNewRunsAgain()
    {
        var u = await NewPayee("Người A", verified: false); // PENDING_VERIFICATION, tài khoản 0123456789
        await AddLine(u, 500_000, 0);
        var plan = await RunOne(u);
        Assert.Equal("HELD", plan.Status);
        Assert.Equal(0, Fake.PayoutCalls);

        var verify = $"/api/admin/money/payees/{u}/bank/verify";
        var ok = await AsAdmin("POST", verify, new { expectedAccountNumber = "0123456789" });
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal("VERIFIED", Data(ok.Body).GetProperty("status").GetString());
        Assert.Contains("giải phóng 1 kỳ", ok.Body.GetProperty("message").GetString());
        var run = await RunOf(plan.RunId!.Value);
        Assert.Equal("PROCESSING", run.Status); // đã giải phóng và gửi lệnh chi
        Assert.Equal(500_000, Fake.Calls.Single().Amount);
        await using (var db = Open())
        {
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == u && n.Title == "Tài khoản ngân hàng đã được duyệt"));
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "PAYOUT_RELEASED" && a.EntityId == run.Id));
        }
        // duyệt lại khi đã VERIFIED: không giải phóng thêm, không báo thêm
        var calls = Fake.PayoutCalls;
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("POST", verify, new { expectedAccountNumber = "0123456789" })).Status);
        Assert.Equal(calls, Fake.PayoutCalls);
        await using (var db = Open()) Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == u && n.Title == "Tài khoản ngân hàng đã được duyệt"));

        // đổi tài khoản sau khi đã duyệt: về chờ duyệt, kỳ mới bị giữ, kỳ đang chạy không bị đụng
        var changed = await Call("PUT", "/api/money/my/bank", "SELLER", u, BankBody("777888999", "NGUYEN VAN AN"));
        Assert.Equal("PENDING_VERIFICATION", Data(changed.Body).GetProperty("status").GetString());
        await AddLine(u, 300_000, 0, availableAt: PEnd.AddDays(1));
        var second = await RunOne(u, PEnd, PEnd.AddDays(3));
        Assert.Equal("HELD", second.Status);
        Assert.Equal("PROCESSING", (await RunOf(plan.RunId!.Value)).Status);
        Assert.Equal(calls, Fake.PayoutCalls);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", verify, new { expectedAccountNumber = "0123456789" })).Status); // số cũ: guard
        var ok2 = await AsAdmin("POST", verify, new { expectedAccountNumber = "777888999" });
        Assert.Equal(HttpStatusCode.OK, ok2.Status);
        Assert.Equal("PROCESSING", (await RunOf(second.RunId!.Value)).Status);
        Assert.Equal(calls + 1, Fake.PayoutCalls);
        Assert.Equal(300_000, Fake.Calls.Last().Amount);
        Assert.Contains("777888999", Fake.Calls.Last().ToAccountNumber); // dùng tài khoản đã duyệt mới
    }

    [PostgresMoneyFact]
    public async Task VerifyDoesNotReleaseRunsOfOtherPayees()
    {
        var a = await NewPayee("Người A", verified: false); var b = await NewPayee("Người B", verified: false);
        await AddLine(a, 500_000, 0); await AddLine(b, 500_000, 0);
        var pa = await RunOne(a); var pb = await RunOne(b);
        Assert.Equal("HELD", pa.Status); Assert.Equal("HELD", pb.Status);
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("POST", $"/api/admin/money/payees/{a}/bank/verify", new { expectedAccountNumber = "0123456789" })).Status);
        Assert.Equal("PROCESSING", (await RunOf(pa.RunId!.Value)).Status);
        Assert.Equal("HELD", (await RunOf(pb.RunId!.Value)).Status);
        Assert.Equal(1, Fake.PayoutCalls);
    }

    // ============================ BẤT THƯỜNG THU TIỀN ============================

    [PostgresMoneyFact]
    public async Task AnomalyListFiltersPagingOrderingResolveAndSummary()
    {
        var owner = await NewUser("DEPOT_OWNER", "Chủ kho trả"); var seller = await NewPayee("Người bán");
        var c1 = await NewCollectionFor(owner, seller, "PICKUP"); var c2 = await NewCollectionFor(owner, seller, "BATCH"); var c3 = await NewCollectionFor(owner, seller, "LIQUIDATION_BALANCE");
        var a1 = await NewAnomalyFor(c1, "OVERPAID", 50_000, at: T0.AddHours(-3));
        var a2 = await NewAnomalyFor(c2, "DUPLICATE_PAYMENT", 60_000, at: T0.AddHours(-2));
        var a3 = await NewAnomalyFor(c3, "LATE_PAYMENT", 70_000, at: T0.AddHours(-1));
        var a4 = await NewAnomalyFor(c1, "UNDERPAID", 80_000, status: "RESOLVED", at: T0);

        var all = Data((await AsAdmin("GET", "/api/admin/money/anomalies")).Body);
        Assert.Equal(4, all.GetProperty("totalCount").GetInt32());
        Assert.Equal([a4, a3, a2, a1], all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray()); // created_at giảm dần
        Assert.Equal(3, Data((await AsAdmin("GET", "/api/admin/money/anomalies?status=open")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, Data((await AsAdmin("GET", "/api/admin/money/anomalies?kind=OVERPAID")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, Data((await AsAdmin("GET", "/api/admin/money/anomalies?flow=BATCH")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(2, Data((await AsAdmin("GET", "/api/admin/money/anomalies?flow=PICKUP")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, Data((await AsAdmin("GET", "/api/admin/money/anomalies?status=OPEN&flow=PICKUP")).Body).GetProperty("totalCount").GetInt32());
        var page2 = Data((await AsAdmin("GET", "/api/admin/money/anomalies?page=2&pageSize=2")).Body);
        Assert.Equal([a2, a1], page2.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(100, Data((await AsAdmin("GET", "/api/admin/money/anomalies?pageSize=1000")).Body).GetProperty("pageSize").GetInt32());

        var item = Data((await AsAdmin("GET", $"/api/admin/money/anomalies/{a2}")).Body);
        Assert.Equal("DUPLICATE_PAYMENT", item.GetProperty("kind").GetString());
        Assert.Equal("OPEN", item.GetProperty("status").GetString());
        Assert.Equal(60_000, item.GetProperty("refundAmount").GetDecimal());
        Assert.Equal(60_000, item.GetProperty("refundable").GetDecimal());
        Assert.False(item.GetProperty("hasRefund").GetBoolean());
        Assert.Equal("BATCH", item.GetProperty("collection").GetProperty("flow").GetString());
        Assert.Equal(c2, item.GetProperty("collection").GetProperty("id").GetGuid());
        Assert.Equal(1_000_000, item.GetProperty("collection").GetProperty("amount").GetDecimal());
        Assert.Equal("PAID", item.GetProperty("collection").GetProperty("status").GetString());
        Assert.Equal("Chủ kho trả", item.GetProperty("payer").GetProperty("name").GetString());
        Assert.Equal(owner, item.GetProperty("payer").GetProperty("id").GetGuid());
        Assert.Equal("Người bán", item.GetProperty("payee").GetProperty("name").GetString());
        Assert.Equal("VERIFIED", item.GetProperty("payerBank").GetProperty("status").GetString());
        Assert.Equal("****6789", item.GetProperty("payerBank").GetProperty("accountMasked").GetString());
        Assert.DoesNotContain("0123456789", (await AsAdmin("GET", "/api/admin/money/anomalies")).Raw);
        Assert.Equal(HttpStatusCode.NotFound, (await AsAdmin("GET", $"/api/admin/money/anomalies/{Guid.NewGuid()}")).Status);

        Assert.Equal(3, Data((await AsAdmin("GET", "/api/admin/money/anomalies/summary")).Body).GetProperty("openCount").GetInt32());
        var resolve = $"/api/admin/money/anomalies/{a1}/resolve";
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", resolve, new { note = "ok" })).Status);
        var done = await AsAdmin("POST", resolve, new { note = "Đã hoàn tiền ngoài hệ thống" });
        Assert.Equal(HttpStatusCode.OK, done.Status);
        Assert.Equal("RESOLVED", Data(done.Body).GetProperty("status").GetString());
        Assert.Equal(AdminUser, Data(done.Body).GetProperty("resolvedBy").GetGuid());
        Assert.Equal(0, Data(done.Body).GetProperty("refundable").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", resolve, new { note = "Đóng lần hai" })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await AsAdmin("POST", $"/api/admin/money/anomalies/{Guid.NewGuid()}/resolve", new { note = "Không tồn tại" })).Status);
        Assert.Equal(2, Data((await AsAdmin("GET", "/api/admin/money/anomalies/summary")).Body).GetProperty("openCount").GetInt32());
        await using (var db = Open())
        {
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(x => x.Action == "ANOMALY_RESOLVED" && x.EntityId == a1);
            Assert.Equal(AdminUser, audit.UserId);
            Assert.Equal("Đã hoàn tiền ngoài hệ thống", JsonDocument.Parse(audit.NewData!).RootElement.GetProperty("note").GetString());
        }

        // đã có lệnh hoàn thì hiển thị và không đóng thủ công được
        var refund = await AsAdmin("POST", $"/api/admin/money/anomalies/{a2}/refund", new { reason = "Hoàn tiền chuyển trùng" });
        Assert.Equal(HttpStatusCode.OK, refund.Status);
        var after = Data((await AsAdmin("GET", $"/api/admin/money/anomalies/{a2}")).Body);
        Assert.True(after.GetProperty("hasRefund").GetBoolean());
        Assert.Equal(0, after.GetProperty("refundable").GetDecimal());
        Assert.Equal("PROCESSING", after.GetProperty("refundStatus").GetString());
        Assert.StartsWith("RF-", after.GetProperty("refundCode").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/anomalies/{a2}/resolve", new { note = "Đóng thủ công" })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/anomalies/{a2}/refund", new { reason = "Hoàn lần hai" })).Status);
    }

    // ============================ LỆNH HOÀN TIỀN ============================

    [PostgresMoneyFact]
    public async Task RefundEndpointsCreateListDetailAndTransitionsOverHttp()
    {
        var owner = await NewUser("DEPOT_OWNER", "Chủ kho trả"); var seller = await NewPayee("Người bán");
        var c = await NewCollectionFor(owner, seller);
        var a = await NewAnomalyFor(c, "OVERPAID", 200_000);

        var bad = await AsAdmin("POST", $"/api/admin/money/anomalies/{a}/refund", new { amount = 200_001, reason = "Hoàn phần dư" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await AsAdmin("POST", $"/api/admin/money/anomalies/{Guid.NewGuid()}/refund", new { reason = "Hoàn phần dư" })).Status);
        var created = await AsAdmin("POST", $"/api/admin/money/anomalies/{a}/refund", new { amount = 150_000, reason = "Hoàn phần dư" });
        Assert.Equal(HttpStatusCode.OK, created.Status);
        var d = Data(created.Body);
        var r = d.GetProperty("refund");
        var id = r.GetProperty("id").GetGuid();
        Assert.Equal("PROCESSING", r.GetProperty("status").GetString());
        Assert.Equal("EXCESS", r.GetProperty("kind").GetString());
        Assert.Equal(150_000, r.GetProperty("amount").GetDecimal());
        Assert.Equal("Chủ kho trả", r.GetProperty("payerName").GetString());
        Assert.Equal(a, r.GetProperty("anomalyId").GetGuid());
        Assert.Equal(r.GetProperty("code").GetString(), d.GetProperty("idempotencyKey").GetString());
        Assert.Equal("****6789", d.GetProperty("bank").GetProperty("accountMasked").GetString());
        Assert.Equal("OVERPAID", d.GetProperty("anomalyKind").GetString());
        Assert.Equal(c, d.GetProperty("collection").GetProperty("id").GetGuid());
        Assert.DoesNotContain("0123456789", created.Raw);

        var list = Data((await AsAdmin("GET", "/api/admin/money/refunds")).Body);
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, Data((await AsAdmin("GET", "/api/admin/money/refunds?status=processing")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, Data((await AsAdmin("GET", "/api/admin/money/refunds?status=PAID")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, Data((await AsAdmin("GET", $"/api/admin/money/refunds?payerId={owner}")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, Data((await AsAdmin("GET", $"/api/admin/money/refunds?payerId={Guid.NewGuid()}")).Body).GetProperty("totalCount").GetInt32());
        Assert.Equal(id, Data((await AsAdmin("GET", $"/api/admin/money/refunds/{id}")).Body).GetProperty("refund").GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await AsAdmin("GET", $"/api/admin/money/refunds/{Guid.NewGuid()}")).Status);

        // không duyệt/gửi lại/hủy được lệnh đang chạy; đối soát → PAID; đóng bất thường tự động
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/refunds/{id}/approve")).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/refunds/{id}/retry")).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/refunds/{id}/cancel", new { })).Status);
        Fake.Status = new(Retrack.API.Services.Money.PayoutStatus.Succeeded, null);
        var paid = await AsAdmin("POST", $"/api/admin/money/refunds/{id}/reconcile");
        Assert.Equal("PAID", Data(paid.Body).GetProperty("refund").GetProperty("status").GetString());
        Assert.Equal("RESOLVED", Data((await AsAdmin("GET", $"/api/admin/money/anomalies/{a}")).Body).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/refunds/{id}/mark-paid", new { reference = "BANK-1", confirm = true })).Status);

        // hoàn khoản thu: kiểm tra đầu vào qua HTTP
        var c2 = await NewCollectionFor(owner, seller, amount: 400_000);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", $"/api/admin/money/collections/{c2}/refund", new { kind = "EXCESS", reason = "Đơn bị hủy" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", $"/api/admin/money/collections/{c2}/refund", new { kind = "PARTIAL", reason = "Đơn bị hủy" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", $"/api/admin/money/collections/{c2}/refund", new { kind = "PARTIAL", amount = 400_001, reason = "Đơn bị hủy" })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await AsAdmin("POST", $"/api/admin/money/collections/{Guid.NewGuid()}/refund", new { kind = "FULL", reason = "Đơn bị hủy" })).Status);
        var partial = await AsAdmin("POST", $"/api/admin/money/collections/{c2}/refund", new { kind = "PARTIAL", amount = 100_000, reason = "Đơn bị hủy một phần" });
        Assert.Equal(HttpStatusCode.OK, partial.Status);
        Assert.Equal("PARTIAL", Data(partial.Body).GetProperty("refund").GetProperty("kind").GetString());
        Assert.Equal(c2, Data(partial.Body).GetProperty("refund").GetProperty("collectionId").GetGuid());
        var full = await AsAdmin("POST", $"/api/admin/money/collections/{c2}/refund", new { kind = "FULL", reason = "Hoàn nốt phần còn lại" });
        Assert.Equal(300_000, Data(full.Body).GetProperty("refund").GetProperty("amount").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/collections/{c2}/refund", new { kind = "PARTIAL", amount = 1, reason = "Hoàn thêm một ít" })).Status);
    }

    [PostgresMoneyFact]
    public async Task ApproveCancelAndMarkPaidOverHttpWithThreshold()
    {
        await SetConfig(Retrack.API.Services.Money.MoneyConfigKeys.PayoutAutoMaxAmount, "100000");
        var owner = await NewUser("FACTORY", "Nhà máy trả"); var seller = await NewPayee("Người bán");
        var a1 = await NewAnomalyFor(await NewCollectionFor(owner, seller), "OVERPAID", 300_000);
        var a2 = await NewAnomalyFor(await NewCollectionFor(owner, seller), "OVERPAID", 300_000);
        var r1 = Data((await AsAdmin("POST", $"/api/admin/money/anomalies/{a1}/refund", new { reason = "Hoàn phần dư lớn" })).Body).GetProperty("refund");
        var r2 = Data((await AsAdmin("POST", $"/api/admin/money/anomalies/{a2}/refund", new { reason = "Hoàn phần dư lớn" })).Body).GetProperty("refund");
        Assert.Equal("AWAITING_APPROVAL", r1.GetProperty("status").GetString());
        Assert.Equal(0, Fake.PayoutCalls);
        Assert.Equal(2, Data((await AsAdmin("GET", "/api/admin/money/anomalies/summary")).Body).GetProperty("refundsAwaitingApproval").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("POST", $"/api/admin/money/refunds/{r1.GetProperty("id").GetGuid()}/approve", "FACTORY", owner)).Status);
        var approved = await AsAdmin("POST", $"/api/admin/money/refunds/{r1.GetProperty("id").GetGuid()}/approve");
        Assert.Equal("PROCESSING", Data(approved.Body).GetProperty("refund").GetProperty("status").GetString());
        Assert.Equal(AdminUser, Data(approved.Body).GetProperty("refund").GetProperty("approvedBy").GetGuid());
        Assert.Equal(1, Fake.PayoutCalls);
        var cancelled = await AsAdmin("POST", $"/api/admin/money/refunds/{r2.GetProperty("id").GetGuid()}/cancel", new { note = "Admin hủy do nhập nhầm" });
        Assert.Equal("CANCELLED", Data(cancelled.Body).GetProperty("refund").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/refunds/{r2.GetProperty("id").GetGuid()}/approve")).Status);
        // đang PROCESSING: mark-paid cần confirm
        var id1 = r1.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/refunds/{id1}/mark-paid", new { reference = "BANK-9", note = "chuyển tay" })).Status);
        var marked = await AsAdmin("POST", $"/api/admin/money/refunds/{id1}/mark-paid", new { reference = "BANK-9", note = "chuyển tay", confirm = true });
        Assert.Equal("MANUAL_PAID", Data(marked.Body).GetProperty("refund").GetProperty("status").GetString());
        Assert.Equal("BANK-9", Data(marked.Body).GetProperty("refund").GetProperty("providerRef").GetString());
        // anomalies/{a2} hủy lệnh → được tạo lại
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("POST", $"/api/admin/money/anomalies/{a2}/refund", new { reason = "Tạo lại sau khi hủy" })).Status);
    }

    [PostgresMoneyFact]
    public async Task RefundAndAnomalyEndpointsRejectAnonymousAndNonAdmin()
    {
        var seller = await NewPayee();
        var id = Guid.NewGuid();
        string[] gets = ["/api/admin/money/anomalies", "/api/admin/money/anomalies/summary", $"/api/admin/money/anomalies/{id}", "/api/admin/money/refunds", $"/api/admin/money/refunds/{id}"];
        foreach (var path in gets)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Call("GET", path, null, null)).Status);
            foreach (var role in new[] { "SELLER", "DEPOT_OWNER", "FACTORY", "DEPOT_EMPLOYEE", "DRIVER" })
                Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", path, role, seller)).Status);
        }
        string[] posts = [$"/api/admin/money/anomalies/{id}/resolve", $"/api/admin/money/anomalies/{id}/refund", $"/api/admin/money/collections/{id}/refund", $"/api/admin/money/refunds/{id}/approve",
            $"/api/admin/money/refunds/{id}/retry", $"/api/admin/money/refunds/{id}/reconcile", $"/api/admin/money/refunds/{id}/cancel", $"/api/admin/money/refunds/{id}/mark-paid"];
        foreach (var path in posts)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Call("POST", path, null, null, new { })).Status);
            foreach (var role in new[] { "SELLER", "DEPOT_OWNER", "FACTORY", "DEPOT_EMPLOYEE", "DRIVER" })
                Assert.Equal(HttpStatusCode.Forbidden, (await Call("POST", path, role, seller, new { })).Status);
        }
        foreach (var path in new[] { "/api/admin/money/payees/pending-bank-count" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", path, "SELLER", seller)).Status);
            Assert.Equal(HttpStatusCode.OK, (await AsAdmin("GET", path)).Status);
        }
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("GET", "/api/admin/money/anomalies")).Status);
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("GET", "/api/admin/money/refunds")).Status);
    }

    [PostgresMoneyFact]
    public async Task PayerSeesOnlyOwnRefundsWithoutProviderInternals()
    {
        var owner = await NewUser("DEPOT_OWNER", "Chủ kho trả"); var factory = await NewUser("FACTORY", "Nhà máy trả"); var seller = await NewPayee("Người bán");
        var a1 = await NewAnomalyFor(await NewCollectionFor(owner, seller), "OVERPAID", 50_000);
        var a2 = await NewAnomalyFor(await NewCollectionFor(factory, seller, "BATCH"), "DUPLICATE_PAYMENT", 70_000);
        await AsAdmin("POST", $"/api/admin/money/anomalies/{a1}/refund", new { reason = "Hoàn phần dư của chủ kho" });
        await AsAdmin("POST", $"/api/admin/money/anomalies/{a2}/refund", new { reason = "Hoàn khoản trả trùng nhà máy" });

        var mine = await Call("GET", "/api/money/my/refunds", "DEPOT_OWNER", owner);
        Assert.Equal(HttpStatusCode.OK, mine.Status);
        var d = Data(mine.Body);
        Assert.Equal(1, d.GetProperty("totalCount").GetInt32());
        var item = d.GetProperty("items")[0];
        Assert.Equal(50_000, item.GetProperty("amount").GetDecimal());
        Assert.Equal("PROCESSING", item.GetProperty("status").GetString());
        Assert.Contains("Đang hoàn tiền", item.GetProperty("statusNote").GetString());
        Assert.StartsWith("RF-", item.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("paidAt").ValueKind);
        Assert.Equal(["amount", "code", "createdAt", "id", "paidAt", "status", "statusNote"], item.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.DoesNotContain("0123456789", mine.Raw);
        Assert.DoesNotContain("providerRef", mine.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reason", mine.Raw, StringComparison.OrdinalIgnoreCase);

        var theirs = Data((await Call("GET", "/api/money/my/refunds", "FACTORY", factory)).Body);
        Assert.Equal(1, theirs.GetProperty("totalCount").GetInt32());
        Assert.Equal(70_000, theirs.GetProperty("items")[0].GetProperty("amount").GetDecimal());
        Assert.Equal(0, Data((await Call("GET", "/api/money/my/refunds", "SELLER", seller)).Body).GetProperty("totalCount").GetInt32()); // người nhận không thấy hoàn tiền của người trả
        Assert.Equal(0, Data((await Call("GET", "/api/money/my/refunds?page=2&pageSize=1", "DEPOT_OWNER", owner)).Body).GetProperty("items").GetArrayLength());

        Fake.Status = new(Retrack.API.Services.Money.PayoutStatus.Succeeded, null);
        await AsAdmin("POST", $"/api/admin/money/refunds/{Data((await AsAdmin("GET", $"/api/admin/money/refunds?payerId={owner}")).Body).GetProperty("items")[0].GetProperty("id").GetGuid()}/reconcile");
        var after = Data((await Call("GET", "/api/money/my/refunds", "DEPOT_OWNER", owner)).Body).GetProperty("items")[0];
        Assert.Equal("PAID", after.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, after.GetProperty("paidAt").ValueKind);
        Assert.Contains("Đã hoàn", after.GetProperty("statusNote").GetString());

        // xác thực/phân quyền
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call("GET", "/api/money/my/refunds", null, null)).Status);
        foreach (var role in new[] { "ADMIN", "DEPOT_EMPLOYEE", "DRIVER" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", "/api/money/my/refunds", role, owner)).Status);
        var inactive = await NewUser("DEPOT_OWNER", "Chủ kho bị khóa", active: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", "/api/money/my/refunds", "DEPOT_OWNER", inactive)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", "/api/money/my/refunds", "DEPOT_OWNER", Guid.NewGuid())).Status); // token còn hạn nhưng không có người dùng
    }

    [PostgresMoneyFact]
    public async Task AdminRefundRefusesWhenPayerBankNotApprovedOverHttp()
    {
        var owner = await NewUser("DEPOT_OWNER", "Chủ kho trả", verified: false); var seller = await NewPayee("Người bán");
        var a = await NewAnomalyFor(await NewCollectionFor(owner, seller), "OVERPAID", 50_000);
        var refused = await AsAdmin("POST", $"/api/admin/money/anomalies/{a}/refund", new { reason = "Hoàn phần dư" });
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Contains("Admin duyệt", refused.Body.GetProperty("message").GetString());
        Assert.Equal(0, Fake.PayoutCalls);
        // người trả nhập tài khoản, Admin duyệt → hoàn được
        Assert.Equal(HttpStatusCode.OK, (await Call("PUT", "/api/money/my/bank", "DEPOT_OWNER", owner, BankBody("246813579", "CHU KHO TRA"))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/anomalies/{a}/refund", new { reason = "Hoàn phần dư" })).Status); // vẫn chờ duyệt
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("POST", $"/api/admin/money/payees/{owner}/bank/verify", new { expectedAccountNumber = "246813579" })).Status);
        var ok = await AsAdmin("POST", $"/api/admin/money/anomalies/{a}/refund", new { reason = "Hoàn phần dư" });
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal("246813579", Fake.Calls.Single().ToAccountNumber);
    }
}
