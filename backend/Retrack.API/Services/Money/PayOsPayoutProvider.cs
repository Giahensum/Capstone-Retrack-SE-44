using Microsoft.Extensions.Logging;

namespace Retrack.API.Services.Money;

/// <summary>
/// Bộ chuyển <see cref="IPayOsPayoutClient"/> (client HTTP của payOS) sang <see cref="IPayoutProvider"/> của engine chi.
/// Không có logic nghiệp vụ ở đây: chỉ ánh xạ kết quả. Lỗi mơ hồ luôn thành Unknown, không bao giờ thành DefiniteFailure.
/// </summary>
public sealed class PayOsPayoutProvider(IPayOsPayoutClient client, ILogger<PayOsPayoutProvider> log) : IPayoutProvider
{
    public string Name => "PAYOS";
    public bool IsConfigured => client.IsConfigured;

    public async Task<PayoutResult> PayoutAsync(PayoutRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0 || request.Amount != decimal.Truncate(request.Amount))
            return new(PayoutOutcome.DefiniteFailure, null, "INVALID_AMOUNT", "Số tiền phải là số nguyên đồng lớn hơn 0.");
        try
        {
            var r = await client.CreatePayoutAsync(request.IdempotencyKey, request.ReferenceId, (long)request.Amount, request.Description,
                request.ToBin, request.ToAccountNumber, null, ct);
            var outcome = r.Outcome switch
            {
                PayOsPayoutOutcome.Accepted => PayoutOutcome.Accepted,
                PayOsPayoutOutcome.DefiniteFailure => PayoutOutcome.DefiniteFailure,
                _ => PayoutOutcome.Unknown,
            };
            return new(outcome, r.ProviderRef, r.Code, r.Desc);
        }
        catch (Exception ex)
        {
            // Hủy/timeout/lỗi lạ sau khi có thể đã gửi: không biết lệnh đã được nhận chưa.
            log.LogWarning(ex, "Gọi lệnh chi payOS thất bại, coi là kết quả chưa rõ ({Reference}).", request.ReferenceId);
            return new(PayoutOutcome.Unknown, null, "EXCEPTION", "Không nhận được phản hồi từ payOS.");
        }
    }

    /// <summary>Tra theo referenceId trước; nếu không thấy thì thử như mã lệnh chi của payOS. Tra cứu lỗi thì ném lỗi (không đoán).</summary>
    public async Task<PayoutStatusResult> GetPayoutAsync(string referenceOrProviderRef, CancellationToken ct = default)
    {
        var s = await client.FindByReferenceAsync(referenceOrProviderRef, ct);
        if (s.State == PayOsPayoutState.NotFound) s = await client.GetPayoutAsync(referenceOrProviderRef, ct);
        return s.State switch
        {
            PayOsPayoutState.Processing => new(PayoutStatus.Processing, s.PayoutId, s.Desc),
            PayOsPayoutState.Succeeded => new(PayoutStatus.Succeeded, s.PayoutId, s.Desc),
            PayOsPayoutState.Failed => new(PayoutStatus.Failed, s.PayoutId, s.Desc ?? s.TransactionState),
            PayOsPayoutState.NotFound => new(PayoutStatus.NotFound, null, s.Desc),
            _ => throw new InvalidOperationException("Không tra cứu được trạng thái lệnh chi từ payOS."),
        };
    }

    public async Task<decimal> GetBalanceAsync(CancellationToken ct = default) =>
        await client.GetBalanceAsync(ct) ?? throw new InvalidOperationException("Không tra cứu được số dư tài khoản chi payOS.");
}
