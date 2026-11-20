namespace Retrack.API.Services.Money;

/// <summary>Một dòng sổ cái trong bảng xem trước/chi tiết kỳ chi. Net là phần dòng này đóng góp vào số chi (REFUND là số âm).</summary>
public sealed record PayoutLineView(Guid LineId, string Flow, string SourceType, Guid SourceId, string Kind, decimal Gross,
    decimal Commission, decimal Rate, decimal Net, DateTime AvailableAt, string State);

/// <summary>
/// Kết quả cho một người nhận khi xem trước hoặc chạy.
/// Outcome: NEW (chỉ xem trước: chạy sẽ tạo kỳ mới), CREATED, EXISTS (kỳ y hệt đã có, idempotent), NO_LINES, CONFLICT (chồng/đi trước kỳ đã có),
/// SKIPPED (tắt tự chi hoặc tài khoản không hoạt động), ERROR. Status là trạng thái kỳ chi (khi xem trước: trạng thái kỳ SẼ nhận).
/// </summary>
public sealed record PayoutPlan(Guid PayeeId, string PayeeName, string Outcome, string? Message, Guid? RunId, string? RunCode,
    IReadOnlyList<PayoutLineView> Lines, decimal Gross, decimal Commission, decimal RefundAmount, decimal CarriedDebt, decimal Net,
    string? Status, string? Reason, string? BankStatus, string? BankMasked);

public sealed record PayoutBatchResult(DateTime PeriodStart, DateTime PeriodEnd, IReadOnlyList<PayoutPlan> Items);

public sealed record TestTransferRequest(string ClientKey, decimal Amount, string ToBin, string ToAccountNumber);

/// <summary>Kết quả chuyển thử. Outcome: ACCEPTED, FAILED, UNKNOWN. Không chứa số tài khoản đầy đủ.</summary>
public sealed record TestTransferResult(string Key, string Provider, string Outcome, string? ProviderRef, string? Message);

public sealed record TestTransferStatus(string Key, string Provider, string Status, string? ProviderRef, string? Message);

public sealed record ProviderBalance(string Provider, bool Configured, string? Balance, string? Error);

public sealed class PayoutWorkerOptions
{
    /// <summary>Chu kỳ kiểm tra lịch tự chi.</summary>
    public TimeSpan AutoInterval { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>Chu kỳ đối soát kỳ chi PROCESSING/UNKNOWN.</summary>
    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Chỉ đối soát kỳ chi có lần gửi cuối cũ hơn khoảng này.</summary>
    public TimeSpan ReconcileMinAge { get; set; } = TimeSpan.FromSeconds(30);
    public int ReconcileBatchSize { get; set; } = 50;
    public bool Enabled { get; set; } = true;
}
