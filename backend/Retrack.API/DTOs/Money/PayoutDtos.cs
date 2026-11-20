using Retrack.API.Services.Money;

namespace Retrack.API.DTOs.Money;

// ===== Cấu hình =====
public sealed record MoneySettingsDto(string Mode, Dictionary<string, decimal> DefaultCommission, bool PayoutAutoEnabled, string PayoutSchedule,
    int PayoutHoldMinutes, decimal PayoutMinAmount, decimal PayoutAutoMaxAmount, string PayoutProvider,
    bool PayoutTestEnabled, decimal PayoutTestMaxAmount, bool PayosPayoutConfigured);

/// <summary>Cập nhật từng phần: trường null là giữ nguyên.</summary>
public sealed class UpdateMoneySettingsDto
{
    public string? Mode { get; set; }
    /// <summary>Khóa PICKUP, BATCH, LIQUIDATION; giá trị 0 đến 100 (%).</summary>
    public Dictionary<string, decimal>? DefaultCommission { get; set; }
    public bool? PayoutAutoEnabled { get; set; }
    public string? PayoutSchedule { get; set; }
    public int? PayoutHoldMinutes { get; set; }
    public decimal? PayoutMinAmount { get; set; }
    public decimal? PayoutAutoMaxAmount { get; set; }
    public string? PayoutProvider { get; set; }
    public bool? PayoutTestEnabled { get; set; }
    public decimal? PayoutTestMaxAmount { get; set; }
}

public sealed record CommissionOverrideDto(Guid UserId, string UserName, string Role, string Flow, decimal Rate, DateTime UpdatedAt);
public sealed record CommissionsDto(Dictionary<string, decimal> Defaults, PagedResult<CommissionOverrideDto> Overrides);
/// <summary>Rate null khi đặt riêng cho người nhận nghĩa là xóa ghi đè (quay về mặc định).</summary>
public sealed class SetCommissionDto { public string Flow { get; set; } = ""; public decimal? Rate { get; set; } }

// ===== Kỳ chi =====
public sealed record PayoutRunListDto(Guid Id, string Code, Guid PayeeId, string PayeeName, DateTime PeriodStart, DateTime PeriodEnd, decimal Gross,
    decimal Commission, decimal RefundAmount, decimal CarriedDebt, decimal Net, string Status, string Provider, string? ProviderRef, string? GatewayStatus,
    string? Reason, int Attempt, DateTime CreatedAt, DateTime? PaidAt, DateTime? ApprovedAt, DateTime? LastAttemptAt);

public sealed record PayoutBankView(string? BankName, string? Bin, string? AccountMasked, string? Holder);

public sealed record PayoutRunDetailDto(PayoutRunListDto Run, IReadOnlyList<PayoutLineView> Lines, PayoutBankView? Bank, string? IdempotencyKey);

public sealed class PayoutPreviewRequest
{
    public List<Guid> PayeeIds { get; set; } = [];
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
}

public sealed class MarkPaidRequest
{
    public string Reference { get; set; } = "";
    public string? Note { get; set; }
    /// <summary>Bắt buộc true khi kỳ chi đang PROCESSING hoặc UNKNOWN.</summary>
    public bool Confirm { get; set; }
}

public sealed class TestTransferBody
{
    public string ClientKey { get; set; } = "";
    public decimal Amount { get; set; }
    public string ToBin { get; set; } = "";
    public string ToAccountNumber { get; set; } = "";
}

// ===== Thu / sổ cái =====
public sealed record AdminMoneyCollectionDto(Guid Id, string Flow, string SourceType, Guid SourceId, Guid PayerId, string PayerName, Guid PayeeId, string PayeeName,
    decimal Amount, string Status, DateTime? PaidAt, decimal? ReceivedAmount, DateTime CreatedAt);

public sealed record PayableLineDto(Guid Id, Guid CollectionId, Guid PayeeId, string PayeeName, string Flow, string SourceType, Guid SourceId, string Kind,
    string? RefundKey, decimal Gross, decimal CommissionRate, decimal Commission, decimal Net, string State, DateTime AvailableAt, Guid? PayoutId, DateTime CreatedAt);

// ===== Người nhận =====
public sealed record PayeeBankDto(string? BankName, string? BankBin, string? AccountNumberMasked, string? AccountName, string Status, DateTime? VerifiedAt, DateTime? ChangedAt, string? RejectReason);

public sealed record PayeeSummaryDto(Guid UserId, string FullName, string Role, bool IsActive, decimal HeldAmount, decimal AvailableAmount, decimal InPayoutAmount,
    bool AutoPayoutEnabled, PayeeBankDto Bank);

/// <summary>Chỉ dành cho Admin: có số tài khoản đầy đủ để đối chiếu trước khi xác minh.</summary>
public sealed record PayeeBankAdminDto(Guid UserId, string FullName, string? BankName, string? BankBin, string? AccountNumber, string? AccountName, string Status,
    DateTime? VerifiedAt, DateTime? ChangedAt, bool NameMatchesFullName, string? RejectReason);

public sealed class VerifyBankRequest { public string ExpectedAccountNumber { get; set; } = ""; }
public sealed class RejectBankRequest { public string ExpectedAccountNumber { get; set; } = ""; public string Reason { get; set; } = ""; }
public sealed record PendingBankCountDto(int PendingBankCount);
public sealed class AutoPayoutRequest { public bool AutoEnabled { get; set; } }

public sealed class UpdateBankRequest
{
    public string BankName { get; set; } = "";
    public string BankBin { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
}

// ===== Người nhận xem thu nhập =====
/// <summary>Held: đang giữ; Available: đã được chi nhưng chưa vào kỳ; InPayout: trong kỳ chi chưa xong; Paid: tổng đã chi (kỳ PAID). Số âm nghĩa là sẽ trừ vào kỳ sau.</summary>
public sealed record MyEarningsDto(decimal Held, decimal Available, decimal InPayout, decimal Paid, decimal CarriedDebt, DateTime? NextReleaseAt, string Currency);

public sealed record MyPayoutDto(Guid Id, string Code, DateTime PeriodStart, DateTime PeriodEnd, decimal Gross, decimal Commission, decimal RefundAmount,
    decimal CarriedDebt, decimal Net, string Status, string StatusNote, DateTime CreatedAt, DateTime? PaidAt);

public sealed record BankOptionDto(string Bin, string Name);
