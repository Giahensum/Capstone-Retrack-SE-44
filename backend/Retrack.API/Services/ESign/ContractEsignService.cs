using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.ESign;

/// <param name="Configured">Máy chủ đã có khóa DocuSign chưa.</param>
/// <param name="State">NOT_STARTED, SENT, FACTORY_SIGNED, DEPOT_SIGNED, COMPLETED, DECLINED hoặc VOIDED.</param>
/// <param name="Role">FACTORY hoặc DEPOT: bên của người đang gọi.</param>
public sealed record EsignStatusDto(bool Configured, string State, string Role, bool MySideSigned, bool FactorySigned, bool DepotSigned,
    string ContractStatus, string? ContractCode, DateTime? SentAt, DateTime? CompletedAt);

public sealed record EsignSigningUrlDto(string Url);

public interface IContractEsignService
{
    Task<EsignStatusDto> GetStatusAsync(Guid userId, Guid partnershipId, CancellationToken ct);
    /// <summary>Tạo envelope nếu chưa có rồi trả địa chỉ trang ký nhúng của bên người gọi.</summary>
    Task<EsignSigningUrlDto> CreateSigningUrlAsync(Guid userId, Guid partnershipId, CancellationToken ct);
    /// <summary>Đọc lại trạng thái từ DocuSign và cập nhật hợp đồng (ký đủ thì hợp đồng có hiệu lực).</summary>
    Task<EsignStatusDto> RefreshAsync(Guid userId, Guid partnershipId, CancellationToken ct);
    Task<(byte[] Content, string FileName)> DownloadAsync(Guid userId, Guid partnershipId, CancellationToken ct);
    /// <summary>DocuSign báo có thay đổi. Chỉ dùng mã envelope trong nội dung để tra cứu; trạng thái luôn được đọc lại từ DocuSign.</summary>
    Task HandleWebhookAsync(string rawBody, CancellationToken ct);
}

/// <summary>
/// Ký điện tử hợp đồng hợp tác Nhà máy – Vựa bằng DocuSign (sandbox). Luồng hợp đồng cũ không đổi: Nhà máy đề nghị (PENDING_DEPOT) →
/// hai bên ký trên DocuSign (ký nhúng trong hệ thống) → ký đủ thì hợp đồng ACTIVE, giống khi chủ vựa chấp thuận. Nút chấp thuận cũ vẫn dùng được.
/// </summary>
public sealed partial class ContractEsignService(
    AppDbContext db, IDocuSignClient docusign, IConfiguration config, ILogger<ContractEsignService> logger) : IContractEsignService
{
    private const string FactoryAnchor = "/ksf/";
    private const string DepotAnchor = "/ksd/";

    public async Task<EsignStatusDto> GetStatusAsync(Guid userId, Guid partnershipId, CancellationToken ct)
    {
        var (p, role) = await LoadAsync(userId, partnershipId, ct);
        return ToDto(p, role);
    }

    public async Task<EsignSigningUrlDto> CreateSigningUrlAsync(Guid userId, Guid partnershipId, CancellationToken ct)
    {
        RequireConfigured();
        var (p, role) = await LoadAsync(userId, partnershipId, ct);
        if (p.IsBlocked) throw new DepotConflictException("Quan hệ đang bị chặn; chưa thể ký hợp đồng.");
        if (p.ContractStatus != "PENDING_DEPOT" || string.IsNullOrWhiteSpace(p.ContractTerms))
            throw new DepotConflictException("Không có hợp đồng đang chờ ký.");

        await EnsureEnvelopeAsync(p, ct);
        // Đồng bộ trước: nếu bên này đã ký rồi thì không mở lại trang ký.
        await SyncAsync(p, ct);
        if (p.ContractStatus != "PENDING_DEPOT") throw new DepotConflictException("Hợp đồng đã được xử lý xong; tải lại trang để xem trạng thái mới.");
        if (IsSideSigned(p, role)) throw new DepotConflictException("Bạn đã ký hợp đồng này; đang chờ bên còn lại.");

        var signer = role == "FACTORY" ? SignerOf(p, true) : SignerOf(p, false);
        var url = await docusign.CreateSigningUrlAsync(p.EsignEnvelopeId!, signer, ReturnUrl(role, p.Id), ct);
        return new EsignSigningUrlDto(url);
    }

    public async Task<EsignStatusDto> RefreshAsync(Guid userId, Guid partnershipId, CancellationToken ct)
    {
        var (p, role) = await LoadAsync(userId, partnershipId, ct);
        if (!string.IsNullOrEmpty(p.EsignEnvelopeId)) { RequireConfigured(); await SyncAsync(p, ct); }
        return ToDto(p, role);
    }

    public async Task<(byte[] Content, string FileName)> DownloadAsync(Guid userId, Guid partnershipId, CancellationToken ct)
    {
        RequireConfigured();
        var (p, _) = await LoadAsync(userId, partnershipId, ct);
        if (string.IsNullOrEmpty(p.EsignEnvelopeId) || p.EsignStatus != "COMPLETED")
            throw new DepotConflictException("Hợp đồng chưa được hai bên ký xong nên chưa có bản đã ký.");
        var pdf = await docusign.DownloadCombinedAsync(p.EsignEnvelopeId, ct);
        return (pdf, $"{p.ContractCode ?? "hop-dong"}-da-ky.pdf");
    }

    public async Task HandleWebhookAsync(string rawBody, CancellationToken ct)
    {
        var match = EnvelopeIdPattern().Match(rawBody ?? "");
        if (!match.Success) return;
        var envelopeId = match.Groups[1].Value.ToLowerInvariant();
        var p = await db.FactoryDepotPartnerships.Include(x => x.Factory).ThenInclude(f => f.Owner).Include(x => x.Depot).ThenInclude(d => d.Owner)
            .SingleOrDefaultAsync(x => x.EsignEnvelopeId == envelopeId, ct);
        if (p is null || !docusign.IsConfigured) return;
        try { await SyncAsync(p, ct); }
        catch (DocuSignException ex) { logger.LogWarning("Webhook DocuSign: chưa đồng bộ được envelope {EnvelopeId}: {Message}", envelopeId, ex.Message); }
    }

    // ------------------------------------------------------------------ nội bộ
    private void RequireConfigured()
    {
        if (!docusign.IsConfigured) throw new DepotConflictException("Máy chủ chưa cấu hình DocuSign nên chưa dùng được ký điện tử.");
    }

    private async Task<(FactoryDepotPartnership P, string Role)> LoadAsync(Guid userId, Guid partnershipId, CancellationToken ct)
    {
        var p = await db.FactoryDepotPartnerships
            .Include(x => x.Factory).ThenInclude(f => f.Owner)
            .Include(x => x.Depot).ThenInclude(d => d.Owner)
            .SingleOrDefaultAsync(x => x.Id == partnershipId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy quan hệ hợp tác.");
        var role = p.Factory.OwnerId == userId ? "FACTORY" : p.Depot.OwnerId == userId ? "DEPOT"
            : throw new DepotForbiddenException("Bạn không thuộc hợp đồng này.");
        return (p, role);
    }

    private static bool IsSideSigned(FactoryDepotPartnership p, string role) => role == "FACTORY"
        ? p.EsignStatus is "FACTORY_SIGNED" or "COMPLETED"
        : p.EsignStatus is "DEPOT_SIGNED" or "COMPLETED";

    private EsignStatusDto ToDto(FactoryDepotPartnership p, string role)
    {
        var state = string.IsNullOrEmpty(p.EsignEnvelopeId) ? "NOT_STARTED" : p.EsignStatus ?? "SENT";
        var factorySigned = state is "FACTORY_SIGNED" or "COMPLETED";
        var depotSigned = state is "DEPOT_SIGNED" or "COMPLETED";
        return new EsignStatusDto(docusign.IsConfigured, state, role, role == "FACTORY" ? factorySigned : depotSigned,
            factorySigned, depotSigned, p.ContractStatus ?? "", p.ContractCode, p.EsignSentAt, p.EsignCompletedAt);
    }

    private static EnvelopeSigner SignerOf(FactoryDepotPartnership p, bool factory)
    {
        var user = factory ? p.Factory.Owner : p.Depot.Owner;
        if (user is null) throw new KeyNotFoundException("Không tìm thấy tài khoản người ký.");
        var id = factory ? p.Factory.OwnerId : p.Depot.OwnerId;
        return new EnvelopeSigner(factory ? "1" : "2", user.Email, user.FullName, id.ToString(), factory ? FactoryAnchor : DepotAnchor);
    }

    private string ReturnUrl(string role, Guid partnershipId)
    {
        var frontend = (config["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');
        var path = role == "FACTORY" ? "/factory/" : "/depot/partners";
        return $"{frontend}{path}?esign=return&partnership={partnershipId}";
    }

    /// <summary>Tạo envelope cho đúng nội dung hợp đồng hiện tại; đề nghị lại (nội dung đổi) hoặc envelope đã bị từ chối thì tạo envelope mới.</summary>
    private async Task EnsureEnvelopeAsync(FactoryDepotPartnership p, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(p.EsignEnvelopeId) && p.EsignTermsHash == p.ContractTermsHash && p.EsignStatus is not ("DECLINED" or "VOIDED")) return;
        var factory = SignerOf(p, true);
        var depot = SignerOf(p, false);
        if (string.Equals(factory.Email, depot.Email, StringComparison.OrdinalIgnoreCase))
            throw new DepotConflictException("Nhà máy và vựa đang dùng cùng một email; DocuSign cần hai người ký khác nhau.");
        var subject = $"Ký hợp đồng hợp tác {p.ContractCode}";
        var envelopeId = await docusign.CreateEnvelopeAsync(subject, p.ContractCode ?? "hop-dong", BuildHtml(p), [factory, depot], ct);
        p.EsignEnvelopeId = envelopeId.ToLowerInvariant();
        p.EsignTermsHash = p.ContractTermsHash;
        p.EsignStatus = "SENT";
        p.EsignSentAt = DateTime.UtcNow;
        p.EsignCompletedAt = null;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Đọc DocuSign rồi ghi vào hợp đồng; khóa dòng vựa như các thao tác hợp đồng khác để không đua với nút chấp thuận cũ.</summary>
    private async Task SyncAsync(FactoryDepotPartnership p, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(p.EsignEnvelopeId)) return;
        var envelope = await docusign.GetEnvelopeAsync(p.EsignEnvelopeId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {p.DepotId} FOR UPDATE").LoadAsync(ct);
        await db.Entry(p).ReloadAsync(ct);

        var now = DateTime.UtcNow;
        var factoryDone = envelope.Signers.FirstOrDefault(s => s.RecipientId == "1");
        var depotDone = envelope.Signers.FirstOrDefault(s => s.RecipientId == "2");
        var factorySigned = factoryDone?.Status == "completed";
        var depotSigned = depotDone?.Status == "completed";

        if (p.ContractStatus == "PENDING_DEPOT")
        {
            if (factorySigned) { p.FactorySignedBy = p.Factory.OwnerId; p.FactorySignedAt = factoryDone!.SignedAt ?? p.FactorySignedAt ?? now; }
            if (envelope.Status == "completed" || (factorySigned && depotSigned))
            {
                p.EsignStatus = "COMPLETED";
                p.EsignCompletedAt = envelope.CompletedAt ?? now;
                p.ContractStatus = "ACTIVE";
                p.Status = "APPROVED";
                p.DepotSignedBy = p.Depot.OwnerId;
                p.DepotSignedAt = depotDone?.SignedAt ?? now;
                p.ContractEffectiveAt = p.EsignCompletedAt;
            }
            else if (envelope.Status == "declined")
            {
                p.EsignStatus = "DECLINED";
                p.ContractStatus = "REJECTED";
                p.Status = "DECLINED";
                p.DepotSignedBy = null;
                p.DepotSignedAt = null;
                p.ContractEffectiveAt = null;
            }
            else if (envelope.Status == "voided") p.EsignStatus = "VOIDED";
            else p.EsignStatus = factorySigned ? "FACTORY_SIGNED" : depotSigned ? "DEPOT_SIGNED" : "SENT";
            p.UpdatedAt = now;
        }
        else if (p.EsignStatus != "COMPLETED" && envelope.Status == "completed")
        {
            // Hợp đồng đã đổi trạng thái bằng đường khác (ví dụ chủ vựa bấm chấp thuận): chỉ ghi nhận kết quả ký, không đổi hợp đồng.
            p.EsignStatus = "COMPLETED";
            p.EsignCompletedAt = envelope.CompletedAt ?? now;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static string BuildHtml(FactoryDepotPartnership p)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var terms = E(p.ContractTerms).Replace("\r\n", "\n").Replace("\n", "<br/>");
        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset=\"utf-8\"/></head><body style=\"font-family:Arial,sans-serif;font-size:13px;line-height:1.5\">");
        sb.Append("<h2 style=\"text-align:center\">HỢP ĐỒNG HỢP TÁC</h2>");
        sb.Append($"<p style=\"text-align:center\">Số: <b>{E(p.ContractCode)}</b> &middot; Phiên bản {p.ContractVersion}</p>");
        sb.Append("<h3>Các bên</h3>");
        sb.Append($"<p><b>Bên A (Nhà máy):</b> {E(p.Factory.Name)}<br/>Địa chỉ: {E(p.Factory.Address)}<br/>Mã số thuế: {E(p.Factory.TaxCode)}<br/>Đại diện: {E(p.Factory.Owner?.FullName)}</p>");
        sb.Append($"<p><b>Bên B (Vựa thu gom):</b> {E(p.Depot.Name)}<br/>Địa chỉ: {E(p.Depot.Address)}<br/>Đại diện: {E(p.Depot.Owner?.FullName)}</p>");
        if (p.ContractExpiresAt is { } expires) sb.Append($"<p>Hiệu lực đến: {expires:dd/MM/yyyy}</p>");
        sb.Append("<h3>Nội dung hợp đồng</h3>");
        sb.Append($"<p>{terms}</p>");
        sb.Append($"<p style=\"font-size:10px;color:#666\">Mã băm SHA-256 của nội dung: {E(p.ContractTermsHash)}</p>");
        sb.Append("<table style=\"width:100%;margin-top:40px\"><tr>");
        sb.Append($"<td style=\"width:50%;vertical-align:top\"><b>ĐẠI DIỆN BÊN A</b><br/>{E(p.Factory.Owner?.FullName)}<br/><br/><br/><span style=\"color:#ffffff;font-size:1px\">{FactoryAnchor}</span></td>");
        sb.Append($"<td style=\"width:50%;vertical-align:top\"><b>ĐẠI DIỆN BÊN B</b><br/>{E(p.Depot.Owner?.FullName)}<br/><br/><br/><span style=\"color:#ffffff;font-size:1px\">{DepotAnchor}</span></td>");
        sb.Append("</tr></table></body></html>");
        return sb.ToString();
    }

    [GeneratedRegex(@"envelope_?id""?\s*[:>]\s*""?([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.IgnoreCase)]
    private static partial Regex EnvelopeIdPattern();
}
