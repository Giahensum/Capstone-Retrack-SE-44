namespace Retrack.API.Services.Money;

/// <param name="ReferenceId">Mã tham chiếu của ta (ví dụ mã kỳ chi). Tra cứu trạng thái bằng mã này.</param>
/// <param name="IdempotencyKey">Gửi lại sau lỗi mơ hồ phải dùng LẠI đúng khóa này, không tạo khóa mới.</param>
/// <param name="Description">Tối đa 20 ký tự (giới hạn payOS).</param>
public sealed record PayoutRequest(string ReferenceId, string IdempotencyKey, decimal Amount, string Description, string ToBin, string ToAccountNumber);

public enum PayoutOutcome
{
    /// <summary>Nhà cung cấp đã nhận lệnh (chưa chắc đã chuyển xong; hỏi GetPayoutAsync).</summary>
    Accepted,
    /// <summary>Từ chối rõ ràng: chắc chắn chưa chuyển tiền, có thể ghi FAILED.</summary>
    DefiniteFailure,
    /// <summary>Timeout/5xx: không biết lệnh đã được nhận chưa. KHÔNG gửi lại bằng khóa mới; đối soát hoặc gửi lại cùng khóa.</summary>
    Unknown,
}

public sealed record PayoutResult(PayoutOutcome Outcome, string? ProviderRef, string? ErrorCode = null, string? Message = null);

public enum PayoutStatus { Processing, Succeeded, Failed, NotFound }

public sealed record PayoutStatusResult(PayoutStatus Status, string? ProviderRef, string? Message = null);

/// <summary>Nhà cung cấp chi tiền. KHÔNG gọi trong transaction DB: ghi PROCESSING trước, gọi sau, ghi kết quả.</summary>
public interface IPayoutProvider
{
    /// <summary>SIMULATED hoặc PAYOS (khớp payout_runs.provider).</summary>
    string Name { get; }
    bool IsConfigured { get; }
    Task<PayoutResult> PayoutAsync(PayoutRequest request, CancellationToken ct = default);
    /// <summary>Tra cứu theo referenceId hoặc providerRef.</summary>
    Task<PayoutStatusResult> GetPayoutAsync(string referenceOrProviderRef, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(CancellationToken ct = default);
}
