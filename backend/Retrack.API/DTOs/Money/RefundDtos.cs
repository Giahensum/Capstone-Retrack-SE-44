namespace Retrack.API.DTOs.Money;

// ===== Bất thường thu tiền (Admin) =====
public sealed record AnomalyCollectionView(Guid Id, string Flow, Guid SourceId, decimal Amount, string Status);
public sealed record PartyView(Guid Id, string Name);

/// <summary>Tài khoản ngân hàng của người trả (che số): Status là VERIFIED, PENDING_VERIFICATION, REJECTED hoặc NONE (chưa nhập).</summary>
public sealed record PartyBankView(string Status, string? BankName, string? AccountMasked);

/// <summary>
/// Refundable: số tiền còn hoàn được (refund_amount nếu bất thường OPEN và chưa có lệnh hoàn hiệu lực, ngược lại 0).
/// HasRefund: đã có lệnh hoàn chưa hủy (RefundId/RefundCode/RefundStatus là lệnh mới nhất chưa hủy).
/// </summary>
public sealed record AdminAnomalyDto(Guid Id, string Kind, string Status, decimal ReceivedAmount, decimal ExpectedAmount, decimal RefundAmount, decimal Refundable,
    bool HasRefund, Guid? RefundId, string? RefundCode, string? RefundStatus, string Reference, long? OrderCode, string? Note, DateTime CreatedAt, DateTime? ResolvedAt,
    Guid? ResolvedBy, AnomalyCollectionView Collection, PartyView Payer, PartyView Payee, PartyBankView PayerBank);

public sealed record AnomalySummaryDto(int OpenCount, int RefundsAwaitingApproval);

public sealed class ResolveAnomalyRequest { public string Note { get; set; } = ""; }

public sealed class CreateAnomalyRefundRequest
{
    /// <summary>Bỏ trống = hoàn toàn bộ refund_amount của bất thường.</summary>
    public decimal? Amount { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class CreateCollectionRefundRequest
{
    /// <summary>FULL (bỏ trống amount) hoặc PARTIAL (bắt buộc amount).</summary>
    public string Kind { get; set; } = "";
    public decimal? Amount { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class CancelRefundRequest { public string? Note { get; set; } }

// ===== Lệnh hoàn tiền (Admin) =====
public sealed record AdminRefundDto(Guid Id, string Code, Guid CollectionId, Guid? AnomalyId, Guid PayerId, string PayerName, string Kind, decimal Amount, string Reason,
    string Status, string Provider, string? ProviderRef, string? GatewayStatus, string? FailReason, int Attempt, Guid? CreatedBy, Guid? ApprovedBy, DateTime? ApprovedAt,
    DateTime CreatedAt, DateTime? LastAttemptAt, DateTime? PaidAt);

/// <summary>Chi tiết: kèm tài khoản đã chốt (che số), khóa idempotency hiện hành và khoản thu gốc.</summary>
public sealed record AdminRefundDetailDto(AdminRefundDto Refund, PayoutBankView? Bank, string? IdempotencyKey, AnomalyCollectionView Collection, string? AnomalyKind, string? LedgerLineState);

// ===== Người trả xem hoàn tiền của mình =====
public sealed record MyRefundDto(Guid Id, string Code, decimal Amount, string Status, string StatusNote, DateTime CreatedAt, DateTime? PaidAt);
