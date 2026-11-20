using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.ESign;

/// <param name="Configured">Máy chủ đã có khóa DocuSign chưa.</param>
/// <param name="State">NOT_STARTED, SENT, SELLER_SIGNED, BUYER_SIGNED, COMPLETED, DECLINED hoặc VOIDED.</param>
/// <param name="Role">SELLER hoặc BUYER (chủ kho mua): bên của người đang gọi.</param>
public sealed record LiquidationEsignStatusDto(bool Configured, string State, string Role, bool MySideSigned, bool SellerSigned, bool BuyerSigned,
    string ContractNumber, DateTime? SentAt, DateTime? CompletedAt, bool LegacyAllowed);

public interface ILiquidationEsignService
{
    Task<LiquidationEsignStatusDto> GetStatusAsync(Guid userId, Guid agreementId, CancellationToken ct);
    /// <summary>Tạo envelope nếu chưa có rồi trả địa chỉ trang ký nhúng của bên người gọi.</summary>
    Task<EsignSigningUrlDto> CreateSigningUrlAsync(Guid userId, Guid agreementId, CancellationToken ct);
    /// <summary>Đọc lại trạng thái từ DocuSign và ghi nhận chữ ký của hai bên vào thỏa thuận.</summary>
    Task<LiquidationEsignStatusDto> RefreshAsync(Guid userId, Guid agreementId, CancellationToken ct);
    Task<(byte[] Content, string FileName)> DownloadAsync(Guid userId, Guid agreementId, CancellationToken ct);
    /// <summary>DocuSign báo có thay đổi. Chỉ dùng mã envelope trong nội dung để tra cứu; trạng thái luôn được đọc lại từ DocuSign.</summary>
    Task HandleWebhookAsync(string rawBody, CancellationToken ct);
    /// <summary>
    /// Nút ký xác nhận cũ chỉ còn dùng khi chưa cấu hình DocuSign, hoặc khi một bên đã ký theo cách cũ trước đó (để không kẹt hợp đồng).
    /// Ném lỗi nếu phải ký bằng DocuSign.
    /// </summary>
    Task EnsureLegacySigningAllowedAsync(Guid userId, Guid agreementId, CancellationToken ct);
}

/// <summary>
/// Ký điện tử hợp đồng đặt cọc mua trọn lô thanh lý (Seller – Chủ kho) bằng DocuSign. Luồng thỏa thuận không đổi: hai bên ký xong thì
/// seller_signed_at và buyer_signed_at được ghi như khi bấm xác nhận, nên các bước cọc/thanh toán/giao nhận giữ nguyên.
/// </summary>
public sealed partial class LiquidationEsignService(
    AppDbContext db, IDocuSignClient docusign, IConfiguration config, ILogger<LiquidationEsignService> logger) : ILiquidationEsignService
{
    private const string SellerAnchor = "/kss/";
    private const string BuyerAnchor = "/ksb/";

    public async Task<LiquidationEsignStatusDto> GetStatusAsync(Guid userId, Guid agreementId, CancellationToken ct)
    {
        var (a, role) = await LoadAsync(userId, agreementId, ct);
        return ToDto(a, role);
    }

    public async Task<EsignSigningUrlDto> CreateSigningUrlAsync(Guid userId, Guid agreementId, CancellationToken ct)
    {
        RequireConfigured();
        var (a, role) = await LoadAsync(userId, agreementId, ct);
        RequireContract(a);
        if (string.IsNullOrEmpty(a.EsignEnvelopeId) && (a.SellerSignedAt != null || a.BuyerSignedAt != null))
            throw new DepotConflictException("Hợp đồng đã được một bên xác nhận ký theo cách cũ; hãy hoàn tất bằng nút xác nhận trên nền tảng.");

        await EnsureEnvelopeAsync(a, ct);
        await SyncAsync(a, ct);
        if (a.SellerSignedAt != null && a.BuyerSignedAt != null) throw new DepotConflictException("Hai bên đã ký xong hợp đồng; tải lại trang để xem trạng thái mới.");
        if (IsSideSigned(a, role)) throw new DepotConflictException("Bạn đã ký hợp đồng này; đang chờ bên còn lại.");

        var signer = await SignerOfAsync(a, role == "SELLER", ct);
        var url = await docusign.CreateSigningUrlAsync(a.EsignEnvelopeId!, signer, ReturnUrl(role, a), ct);
        return new EsignSigningUrlDto(url);
    }

    public async Task<LiquidationEsignStatusDto> RefreshAsync(Guid userId, Guid agreementId, CancellationToken ct)
    {
        var (a, role) = await LoadAsync(userId, agreementId, ct);
        if (!string.IsNullOrEmpty(a.EsignEnvelopeId)) { RequireConfigured(); await SyncAsync(a, ct); }
        return ToDto(a, role);
    }

    public async Task<(byte[] Content, string FileName)> DownloadAsync(Guid userId, Guid agreementId, CancellationToken ct)
    {
        RequireConfigured();
        var (a, _) = await LoadAsync(userId, agreementId, ct);
        if (string.IsNullOrEmpty(a.EsignEnvelopeId) || a.EsignStatus != "COMPLETED")
            throw new DepotConflictException("Hợp đồng chưa được hai bên ký xong nên chưa có bản đã ký.");
        var pdf = await docusign.DownloadCombinedAsync(a.EsignEnvelopeId, ct);
        return (pdf, $"{ContractNumber(a)}-da-ky.pdf");
    }

    public async Task HandleWebhookAsync(string rawBody, CancellationToken ct)
    {
        var match = EnvelopeIdPattern().Match(rawBody ?? "");
        if (!match.Success) return;
        var envelopeId = match.Groups[1].Value.ToLowerInvariant();
        var a = await db.LiquidationAgreements.Include(x => x.Lot).SingleOrDefaultAsync(x => x.EsignEnvelopeId == envelopeId, ct);
        if (a is null || !docusign.IsConfigured) return;
        try { await SyncAsync(a, ct); }
        catch (DocuSignException ex) { logger.LogWarning("Webhook DocuSign: chưa đồng bộ được envelope {EnvelopeId}: {Message}", envelopeId, ex.Message); }
        catch (DepotConflictException) { logger.LogInformation("Bỏ qua kết quả ký đã cũ của hợp đồng thanh lý: {EnvelopeId}", envelopeId); }
    }

    public async Task EnsureLegacySigningAllowedAsync(Guid userId, Guid agreementId, CancellationToken ct)
    {
        var (a, _) = await LoadAsync(userId, agreementId, ct); // người ngoài thỏa thuận bị từ chối như các thao tác khác
        if (IsLegacyAllowed(a)) return;
        throw new DepotConflictException("Hợp đồng đặt cọc được ký điện tử qua DocuSign. Hãy dùng nút “Ký điện tử (DocuSign)”.");
    }

    /// <summary>Ký xác nhận cũ chỉ còn dùng khi chưa bật DocuSign, hoặc khi một bên đã ký cũ trước khi có envelope (tránh kẹt hợp đồng).</summary>
    private bool IsLegacyAllowed(LiquidationAgreement a) =>
        !docusign.IsConfigured || (string.IsNullOrEmpty(a.EsignEnvelopeId) && (a.SellerSignedAt != null || a.BuyerSignedAt != null));

    // ------------------------------------------------------------------ nội bộ
    private void RequireConfigured()
    {
        if (!docusign.IsConfigured) throw new DepotConflictException("Máy chủ chưa cấu hình DocuSign nên chưa dùng được ký điện tử.");
    }

    private static void RequireContract(LiquidationAgreement a)
    {
        if (a.DepositAmount <= 0) throw new DepotConflictException("Thỏa thuận này không có hợp đồng đặt cọc.");
        if (a.DepositReportedAt != null) throw new DepotConflictException("Hợp đồng đã ký xong và đã báo chuyển cọc.");
    }

    private async Task<(LiquidationAgreement A, string Role)> LoadAsync(Guid userId, Guid agreementId, CancellationToken ct)
    {
        var a = await db.LiquidationAgreements.Include(x => x.Lot).SingleOrDefaultAsync(x => x.Id == agreementId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        var role = a.Lot.SellerId == userId ? "SELLER" : a.BuyerId == userId ? "BUYER"
            : throw new DepotForbiddenException("Bạn không thuộc hợp đồng này.");
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct))
            throw new DepotForbiddenException("Tài khoản không còn hoạt động.");
        return (a, role);
    }

    private static bool IsSideSigned(LiquidationAgreement a, string role) => role == "SELLER" ? a.SellerSignedAt != null : a.BuyerSignedAt != null;

    private LiquidationEsignStatusDto ToDto(LiquidationAgreement a, string role)
    {
        var state = string.IsNullOrEmpty(a.EsignEnvelopeId) ? "NOT_STARTED" : a.EsignStatus ?? "SENT";
        var sellerSigned = a.SellerSignedAt != null;
        var buyerSigned = a.BuyerSignedAt != null;
        return new LiquidationEsignStatusDto(docusign.IsConfigured, state, role, role == "SELLER" ? sellerSigned : buyerSigned,
            sellerSigned, buyerSigned, ContractNumber(a), a.EsignSentAt, a.EsignCompletedAt, IsLegacyAllowed(a));
    }

    private async Task<EnvelopeSigner> SignerOfAsync(LiquidationAgreement a, bool seller, CancellationToken ct)
    {
        var id = seller ? a.Lot.SellerId : a.BuyerId;
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new { u.Email, u.FullName }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản người ký.");
        return new EnvelopeSigner(seller ? "1" : "2", user.Email, user.FullName, id.ToString(), seller ? SellerAnchor : BuyerAnchor);
    }

    private string ReturnUrl(string role, LiquidationAgreement a)
    {
        var frontend = (config["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');
        return role == "SELLER"
            ? $"{frontend}/seller/liquidations/{a.LotId}?esign=return&agreement={a.Id}"
            : $"{frontend}/depot/liquidations?view=PURCHASES&lotId={a.LotId}&esign=return&agreement={a.Id}";
    }

    /// <summary>Tạo envelope cho đúng nội dung hợp đồng hiện tại; envelope đã bị từ chối hoặc hủy thì tạo envelope mới.</summary>
    private async Task EnsureEnvelopeAsync(LiquidationAgreement a, CancellationToken ct)
    {
        var documentHash = DocumentHash(a);
        if (!string.IsNullOrEmpty(a.EsignEnvelopeId) && a.EsignTermsHash == documentHash && a.EsignStatus is not ("DECLINED" or "VOIDED")) return;
        var previousEnvelope = a.EsignEnvelopeId;
        var seller = await SignerOfAsync(a, true, ct);
        var buyer = await SignerOfAsync(a, false, ct);
        if (string.Equals(seller.Email, buyer.Email, StringComparison.OrdinalIgnoreCase))
            throw new DepotConflictException("Seller và chủ kho đang dùng cùng một email; DocuSign cần hai người ký khác nhau.");
        var parties = await db.ContractPartiesAsync(a);
        var number = ContractNumber(a);
        var envelopeId = await docusign.CreateEnvelopeAsync($"Ký hợp đồng đặt cọc {number}", number, BuildHtml(a, parties), [seller, buyer], ct);
        // Gọi nhà cung cấp ngoài transaction; chỉ lưu nếu thỏa thuận vẫn ở đúng phiên bản đã gửi.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(a, ct);
        await db.Entry(a).ReloadAsync(ct);
        await db.Entry(a.Lot).ReloadAsync(ct); // làm tươi lô để mã băm phản ánh dữ liệu hiện tại
        if (a.EsignEnvelopeId != previousEnvelope)
        {
            // Bên còn lại vừa tạo envelope cho đúng phiên bản này: dùng envelope đó, envelope thừa của lượt này bị bỏ.
            if (!string.IsNullOrEmpty(a.EsignEnvelopeId) && a.EsignTermsHash == documentHash && a.EsignStatus is not ("DECLINED" or "VOIDED"))
            {
                logger.LogWarning("Envelope thanh lý {EnvelopeId} bị bỏ vì một envelope khác đã được lưu trước.", envelopeId);
                return;
            }
            throw new DepotConflictException("Thỏa thuận đã thay đổi. Hãy tải lại trước khi ký.");
        }
        if (DocumentHash(a) != documentHash || a.DepositReportedAt != null)
            throw new DepotConflictException("Thỏa thuận đã thay đổi. Hãy tải lại trước khi ký.");
        // Envelope thay thế yêu cầu cả hai bên ký lại: xóa chữ ký của envelope cũ (chưa báo cọc nên không vi phạm ràng buộc).
        if (!string.IsNullOrEmpty(previousEnvelope)) { a.SellerSignedAt = null; a.BuyerSignedAt = null; }
        a.EsignEnvelopeId = envelopeId.ToLowerInvariant();
        a.EsignTermsHash = documentHash;
        a.EsignStatus = "SENT";
        a.EsignSentAt = DateTime.UtcNow;
        a.EsignCompletedAt = null;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // Cùng thứ tự khóa depot → lô với các thao tác thỏa thuận khác để không deadlock.
    private async Task LockAsync(LiquidationAgreement a, CancellationToken ct)
    {
        await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {a.DepotId} FOR UPDATE").LoadAsync(ct);
        await db.LiquidationLots.FromSqlInterpolated($"SELECT * FROM liquidation_lots WHERE id = {a.LotId} FOR UPDATE").LoadAsync(ct);
    }

    /// <summary>Đọc DocuSign rồi ghi chữ ký vào thỏa thuận; mỗi bên chỉ được ghi một lần, hai bên ký đủ thì hợp đồng hoàn tất.</summary>
    private async Task SyncAsync(LiquidationAgreement a, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(a.EsignEnvelopeId)) return;
        var envelopeId = a.EsignEnvelopeId;
        var esignHash = a.EsignTermsHash;
        var envelope = await docusign.GetEnvelopeAsync(envelopeId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(a, ct);
        await db.Entry(a).ReloadAsync(ct);
        await db.Entry(a.Lot).ReloadAsync(ct);
        if (a.EsignEnvelopeId != envelopeId || a.EsignTermsHash != esignHash || DocumentHash(a) != esignHash)
            throw new DepotConflictException("Kết quả ký thuộc phiên bản hợp đồng cũ. Hãy tải lại để xem hợp đồng hiện tại.");

        var now = DateTime.UtcNow;
        var sellerDone = envelope.Signers.FirstOrDefault(s => s.RecipientId == "1");
        var buyerDone = envelope.Signers.FirstOrDefault(s => s.RecipientId == "2");
        var sellerSigned = sellerDone?.Status == "completed" || envelope.Status == "completed";
        var buyerSigned = buyerDone?.Status == "completed" || envelope.Status == "completed";
        var title = a.Lot.Title;

        if (envelope.Status == "declined" && a.EsignStatus != "COMPLETED" && a.DepositReportedAt == null)
        {
            // Một bên từ chối: hợp đồng không có hiệu lực, xóa chữ ký đã ghi để lần sau ký lại bằng envelope mới.
            a.EsignStatus = "DECLINED";
            a.SellerSignedAt = null;
            a.BuyerSignedAt = null;
        }
        else if (envelope.Status == "voided" && a.EsignStatus != "COMPLETED") a.EsignStatus = "VOIDED";
        else
        {
            if (sellerSigned && a.SellerSignedAt == null)
            {
                a.SellerSignedAt = sellerDone!.SignedAt ?? now;
                db.Notifications.Add(Notification.ForDepot(a.BuyerId, a.DepotId, "LIQUIDATION", "Seller đã ký hợp đồng đặt cọc",
                    $"Seller đã ký điện tử hợp đồng đặt cọc lô {title}. Hãy ký nếu bạn chưa ký, rồi chuyển cọc ngoài hệ thống.", targetType: "LIQUIDATION", targetId: a.LotId));
            }
            if (buyerSigned && a.BuyerSignedAt == null)
            {
                a.BuyerSignedAt = buyerDone!.SignedAt ?? now;
                db.Notifications.Add(Notification.ForLiquidation(a.Lot.SellerId, a.LotId, "Chủ kho đã ký hợp đồng đặt cọc",
                    $"Chủ kho đã ký điện tử hợp đồng đặt cọc lô {title}."));
            }
            if (a.SellerSignedAt != null && a.BuyerSignedAt != null)
            {
                if (a.EsignStatus != "COMPLETED") a.EsignCompletedAt = envelope.CompletedAt ?? now;
                a.EsignStatus = "COMPLETED";
            }
            else a.EsignStatus = sellerSigned ? "SELLER_SIGNED" : buyerSigned ? "BUYER_SIGNED" : "SENT";
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static string DocumentHash(LiquidationAgreement a) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            a.Id, a.LotId, a.DepotId, a.BuyerId, a.Amount, a.DepositAmount, a.Lot.DepositPercent,
            a.Lot.Title, a.Lot.ScopeDetails, a.Lot.CollectionTerms, a.Lot.Address, a.Lot.CollectionDeadline
        }))).ToLowerInvariant();

    // Giờ Việt Nam (UTC+7) cố định để không phụ thuộc cơ sở dữ liệu múi giờ của máy chủ.
    private static DateTime Vn(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);

    private static string ContractNumber(LiquidationAgreement a) => $"HDDC-{Vn(a.CreatedAt).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)}-{a.Id.ToString("N")[..6].ToUpperInvariant()}";

    private static string BuildHtml(LiquidationAgreement a, DTOs.Liquidation.ContractPartiesDto p)
    {
        static string E(string? s) => WebUtility.HtmlEncode((s ?? "").Replace(SellerAnchor, "").Replace(BuyerAnchor, ""));
        static string D(DateTime d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        static string Money(decimal v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".") + " đồng";
        const string dots = "..............................";
        var date = Vn(a.CreatedAt);
        var number = ContractNumber(a);
        var remaining = a.Amount - a.DepositAmount;
        var sb = new StringBuilder();
        void P(string html) => sb.Append("<p style=\"margin:4px 0\">").Append(html).Append("</p>");
        void H(string text) => sb.Append("<h3 style=\"margin:14px 0 4px\">").Append(E(text)).Append("</h3>");

        sb.Append("<html><head><meta charset=\"utf-8\"/></head><body style=\"font-family:Arial,sans-serif;font-size:12px;line-height:1.45\">");
        sb.Append("<p style=\"text-align:center;margin:0\"><b>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</b><br/><b>Độc lập – Tự do – Hạnh phúc</b></p>");
        sb.Append("<h2 style=\"text-align:center;margin:16px 0 2px\">HỢP ĐỒNG ĐẶT CỌC MUA BÁN TRỌN LÔ TÀI SẢN THANH LÝ</h2>");
        sb.Append($"<p style=\"text-align:center;margin:0\">Số: <b>{E(number)}</b></p>");
        P("<i>– Căn cứ Bộ luật Dân sự năm 2015;</i>");
        P($"<i>– Căn cứ kết quả phiên trả giá lô tài sản “{E(a.Lot.Title)}” trên nền tảng RETRACK kết thúc ngày {D(date)};</i>");
        P("<i>– Căn cứ nhu cầu và khả năng của hai bên.</i>");
        P($"Hôm nay, ngày {date.ToString("dd", System.Globalization.CultureInfo.InvariantCulture)} tháng {date.ToString("MM", System.Globalization.CultureInfo.InvariantCulture)} năm {date.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture)}, hai bên gồm:");
        P("<b>BÊN BÁN</b> (sau đây gọi là Bên A)");
        P($"Ông/Bà: <b>{E(p.SellerName)}</b><br/>Số CCCD/CMND hoặc Mã số thuế: {dots}<br/>Địa chỉ thường trú/trụ sở: {dots}<br/>Điện thoại: {E(p.SellerPhone)}");
        P("<b>BÊN MUA</b> (sau đây gọi là Bên B)");
        P($"Đơn vị/Kho: <b>{E(p.DepotName)}</b><br/>Địa chỉ: {E(p.DepotAddress)}<br/>Mã số thuế: {(string.IsNullOrWhiteSpace(p.DepotTaxCode) ? dots : E(p.DepotTaxCode))}<br/>Người đại diện: <b>{E(p.BuyerRepresentative)}</b><br/>Điện thoại: {E(p.BuyerPhone)}");
        P("Hai bên thống nhất ký kết Hợp đồng đặt cọc mua bán trọn lô tài sản thanh lý (sau đây gọi là “Hợp đồng”) với các điều khoản sau:");

        H("Điều 1. Đối tượng của Hợp đồng");
        P($"1.1. Bên A đồng ý bán và Bên B đồng ý mua trọn lô tài sản thanh lý “<b>{E(a.Lot.Title)}</b>” (sau đây gọi là “Lô hàng”) theo giá trúng của phiên trả giá nêu tại Điều 2.");
        P($"1.2. Phạm vi tài sản theo mô tả của Bên A: {E(a.Lot.ScopeDetails).Replace("\n", "<br/>")}");
        P($"1.3. Địa điểm tài sản: {E(a.Lot.Address)}");
        P("1.4. Bên B xác nhận đã tự khảo sát, hiểu rõ hiện trạng, phạm vi và điều kiện mua cả lô. Số lượng, khối lượng nêu trong mô tả chỉ mang tính ước lượng; giá trúng là giá trọn lô và không điều chỉnh theo khối lượng cân được sau khi nhận hàng.");

        H("Điều 2. Giá mua bán và tiền đặt cọc");
        P($"2.1. Tổng giá trị Hợp đồng (giá trúng trọn lô): <b>{Money(a.Amount)}</b> (Bằng chữ: <i>{VietnameseNumber.AmountInWords(a.Amount)}</i>).");
        P($"2.2. Tiền đặt cọc: <b>{a.Lot.DepositPercent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%</b> giá trúng, tương đương <b>{Money(a.DepositAmount)}</b> (Bằng chữ: <i>{VietnameseNumber.AmountInWords(a.DepositAmount)}</i>). Bên B nộp cho Bên A sau khi hai bên đã ký Hợp đồng này.");
        P($"2.3. Phần còn lại: <b>{Money(remaining)}</b> (Bằng chữ: <i>{VietnameseNumber.AmountInWords(remaining)}</i>). Bên B thanh toán sau khi Bên A xác nhận đã nhận tiền đặt cọc và trước khi Bên A bàn giao Lô hàng.");
        P("2.4. Tiền đặt cọc được trừ vào giá mua khi Hợp đồng được thực hiện.");

        H("Điều 3. Phương thức thanh toán");
        P("3.1. Tiền đặt cọc và tiền mua hàng do Bên B chuyển trực tiếp cho Bên A (chuyển khoản hoặc phương thức khác do hai bên thỏa thuận), thực hiện ngoài nền tảng RETRACK.");
        P("3.2. Mỗi lần chuyển tiền, Bên B ghi rõ nội dung/tham chiếu giao dịch và báo trên nền tảng; Bên A đối chiếu và xác nhận khi đã nhận đủ tiền.");

        H("Điều 4. Giao nhận và thu gom Lô hàng");
        P("4.1. Bên A bàn giao Lô hàng cho Bên B sau khi đã nhận đủ tiền theo Điều 2.");
        P($"4.2. Bên B thu gom hết Lô hàng chậm nhất vào ngày <b>{D(Vn(a.Lot.CollectionDeadline))}</b>.");
        P($"4.3. Điều kiện thu gom và thanh toán do Bên A công bố: {E(a.Lot.CollectionTerms).Replace("\n", "<br/>")}");
        P("4.4. Việc bàn giao của Bên A và việc nhận đủ hàng của Bên B được mỗi bên xác nhận trên nền tảng RETRACK.");

        H("Điều 5. Quyền và nghĩa vụ của Bên A");
        P("5.1. Cung cấp thông tin trung thực về Lô hàng; giữ nguyên hiện trạng Lô hàng cho đến khi bàn giao.");
        P("5.2. Tạo điều kiện để Bên B vào thu gom Lô hàng trong thời hạn Hợp đồng; xác nhận kịp thời việc đã nhận tiền và đã bàn giao.");
        P("5.3. Nhận tiền đặt cọc và tiền mua hàng theo Điều 2.");

        H("Điều 6. Quyền và nghĩa vụ của Bên B");
        P("6.1. Thanh toán đúng số tiền và thời hạn theo Điều 2; thu gom Lô hàng đúng thời hạn theo Điều 4.");
        P("6.2. Tự chịu trách nhiệm tuân thủ quy định pháp luật về an toàn lao động, giao thông và bảo vệ môi trường khi tháo dỡ, vận chuyển và xử lý Lô hàng.");
        P("6.3. Nhận Lô hàng đúng phạm vi và hiện trạng đã khảo sát.");

        H("Điều 7. Xử lý khi vi phạm và tiền đặt cọc");
        P("Trừ khi hai bên có thỏa thuận khác bằng văn bản, việc xử lý tiền đặt cọc áp dụng theo Điều 328 Bộ luật Dân sự năm 2015: nếu Hợp đồng được thực hiện thì tiền đặt cọc được trả lại hoặc trừ vào nghĩa vụ thanh toán; nếu Bên B từ chối thực hiện Hợp đồng thì tiền đặt cọc thuộc về Bên A; nếu Bên A từ chối thực hiện Hợp đồng thì phải trả lại tiền đặt cọc và một khoản tiền tương đương giá trị tiền đặt cọc cho Bên B.");

        H("Điều 8. Vai trò của nền tảng RETRACK");
        P("Nền tảng RETRACK chỉ ghi nhận kết quả phiên trả giá và xác nhận điện tử của hai bên; không phải là một bên của Hợp đồng, không giữ hộ tiền, không bảo lãnh và không xác minh giao dịch ngân hàng của hai bên.");

        H("Điều 9. Giải quyết tranh chấp");
        P("Mọi tranh chấp phát sinh từ Hợp đồng được hai bên giải quyết trên tinh thần thương lượng, hòa giải. Nếu không đạt được thỏa thuận, tranh chấp được giải quyết tại Tòa án nhân dân có thẩm quyền theo quy định của pháp luật Việt Nam.");

        H("Điều 10. Hiệu lực và hình thức Hợp đồng");
        P("10.1. Hợp đồng có hiệu lực kể từ thời điểm cả hai bên ký điện tử qua DocuSign (thời điểm ghi trong chứng nhận hoàn tất của DocuSign).");
        P("10.2. Hợp đồng được lập dưới dạng thông điệp dữ liệu điện tử; bản tải từ nền tảng có giá trị như bản thể hiện của Hợp đồng điện tử.");

        sb.Append($"<p style=\"font-size:10px;color:#666\">Mã băm SHA-256 của nội dung: {E(DocumentHash(a))}</p>");
        sb.Append("<table style=\"width:100%;margin-top:36px\"><tr>");
        sb.Append($"<td style=\"width:50%;vertical-align:top\"><b>ĐẠI DIỆN BÊN A</b><br/>(Ký, ghi rõ họ tên)<br/>{E(p.SellerName)}<br/><br/><br/><span style=\"color:#ffffff;font-size:1px\">{SellerAnchor}</span></td>");
        sb.Append($"<td style=\"width:50%;vertical-align:top\"><b>ĐẠI DIỆN BÊN B</b><br/>(Ký, ghi rõ họ tên)<br/>{E(p.BuyerRepresentative)}<br/><br/><br/><span style=\"color:#ffffff;font-size:1px\">{BuyerAnchor}</span></td>");
        sb.Append("</tr></table>");
        sb.Append($"<p style=\"font-size:10px;color:#666;text-align:center\">Hợp đồng số {E(number)} • Mã thỏa thuận {a.Id}</p>");
        sb.Append("</body></html>");
        return AsciiOnly(sb.ToString());
    }

    /// <summary>DocuSign đổi HTML sang PDF mà không hiểu chắc bảng mã: đổi mọi ký tự không phải ASCII thành mã số HTML để tiếng Việt luôn đúng.</summary>
    private static string AsciiOnly(string html)
    {
        var sb = new StringBuilder(html.Length + 1024);
        foreach (var rune in html.EnumerateRunes())
        {
            if (rune.Value < 128) sb.Append((char)rune.Value);
            else sb.Append("&#").Append(rune.Value).Append(';');
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"envelope_?id""?\s*[:>]\s*""?([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.IgnoreCase)]
    private static partial Regex EnvelopeIdPattern();
}

/// <summary>Đọc số tiền VND thành chữ tiếng Việt, cùng quy tắc với frontend (utils/numberToWords.js).</summary>
public static class VietnameseNumber
{
    private static readonly string[] Digits = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    public static string AmountInWords(decimal amount)
    {
        var n = (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        if (n < 0) return "";
        var text = $"{ReadInteger(n)} đồng";
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string ReadTriple(long n, bool full)
    {
        var hundreds = n / 100; var tens = n % 100 / 10; var units = n % 10;
        var parts = new List<string>();
        if (hundreds > 0 || full) parts.Add($"{Digits[hundreds]} trăm");
        if (tens > 1)
        {
            parts.Add($"{Digits[tens]} mươi");
            if (units == 1) parts.Add("mốt");
            else if (units == 4) parts.Add("tư");
            else if (units == 5) parts.Add("lăm");
            else if (units > 0) parts.Add(Digits[units]);
        }
        else if (tens == 1)
        {
            parts.Add("mười");
            if (units == 5) parts.Add("lăm");
            else if (units > 0) parts.Add(Digits[units]);
        }
        else if (units > 0)
        {
            if (hundreds > 0 || full) parts.Add("lẻ");
            parts.Add(Digits[units]);
        }
        return string.Join(' ', parts);
    }

    private static string ReadInteger(long n)
    {
        if (n == 0) return "không";
        var billions = n / 1_000_000_000;
        var rest = n % 1_000_000_000;
        long[] groups = [rest / 1_000_000, rest % 1_000_000 / 1_000, rest % 1_000];
        string[] labels = ["triệu", "nghìn", ""];
        var output = new List<string>();
        if (billions > 0) output.Add($"{ReadInteger(billions)} tỷ");
        var started = billions > 0;
        for (var i = 0; i < 3; i++)
        {
            if (groups[i] == 0) continue;
            output.Add($"{ReadTriple(groups[i], started)} {labels[i]}".Trim());
            started = true;
        }
        return string.Join(' ', output);
    }
}
