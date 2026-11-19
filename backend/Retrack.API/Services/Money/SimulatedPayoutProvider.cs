namespace Retrack.API.Services.Money;

public sealed class SimulatedPayoutOptions
{
    /// <summary>Thời gian lệnh ở trạng thái Processing trước khi thành Succeeded.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(5);
    public decimal InitialBalance { get; set; } = 100_000_000m;
}

/// <summary>Cách giả lỗi cho lệnh chi kế tiếp (chỉ dùng trong test/demo).</summary>
public enum SimulatedFailure
{
    /// <summary>Từ chối rõ ràng khi gửi: không trừ tiền, không ghi nhận lệnh.</summary>
    RejectAtSubmit,
    /// <summary>Trả Unknown nhưng lệnh CHƯA được ghi nhận (gửi lại cùng khóa sẽ chạy bình thường).</summary>
    UnknownNotRecorded,
    /// <summary>Trả Unknown nhưng lệnh ĐÃ được ghi nhận và sẽ thành công (gửi lại cùng khóa không chi trùng).</summary>
    UnknownRecorded,
    /// <summary>Nhận lệnh nhưng sau độ trễ chuyển thất bại, hoàn lại số dư.</summary>
    FailAfterAccept,
}

/// <summary>
/// Nhà cung cấp chi mô phỏng, mặc định của demo: không chạm tiền thật. Thành công sau một độ trễ cấu hình được,
/// số dư trong bộ nhớ, idempotent theo IdempotencyKey, có giả lỗi cho test. Đăng ký singleton.
/// </summary>
public sealed class SimulatedPayoutProvider(SimulatedPayoutOptions options, TimeProvider clock) : IPayoutProvider
{
    private sealed class Entry
    {
        public required PayoutRequest Request;
        public required string ProviderRef;
        public required DateTimeOffset CompleteAt;
        public bool WillFail;
        public bool Refunded;
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Entry> byKey = new();
    private readonly Dictionary<string, Entry> byReference = new();
    private readonly Queue<SimulatedFailure> injected = new();
    private decimal balance = options.InitialBalance;

    public string Name => "SIMULATED";
    public bool IsConfigured => true;

    /// <summary>Xếp một lỗi giả cho lệnh chi mới kế tiếp (lệnh gửi lại cùng khóa không tiêu thụ lỗi).</summary>
    public void InjectFailure(SimulatedFailure failure) { lock (gate) injected.Enqueue(failure); }

    public void SetBalance(decimal amount) { lock (gate) balance = amount; }

    public Task<PayoutResult> PayoutAsync(PayoutRequest request, CancellationToken ct = default)
    {
        var invalid = Validate(request);
        if (invalid != null) return Task.FromResult(new PayoutResult(PayoutOutcome.DefiniteFailure, null, "INVALID_REQUEST", invalid));
        lock (gate)
        {
            if (byKey.TryGetValue(request.IdempotencyKey, out var same))
            {
                if (same.Request != request) return Task.FromResult(new PayoutResult(PayoutOutcome.DefiniteFailure, null, "IDEMPOTENCY_MISMATCH", "Khóa idempotency đã dùng cho nội dung khác."));
                return Task.FromResult(new PayoutResult(PayoutOutcome.Accepted, same.ProviderRef));
            }
            if (byReference.ContainsKey(request.ReferenceId))
                return Task.FromResult(new PayoutResult(PayoutOutcome.DefiniteFailure, null, "DUPLICATE_REFERENCE", "referenceId đã tồn tại với khóa khác."));

            var failure = injected.Count > 0 ? injected.Dequeue() : (SimulatedFailure?)null;
            if (failure == SimulatedFailure.RejectAtSubmit)
                return Task.FromResult(new PayoutResult(PayoutOutcome.DefiniteFailure, null, "SIMULATED_REJECT", "Giả lập từ chối."));
            if (failure == SimulatedFailure.UnknownNotRecorded)
                return Task.FromResult(new PayoutResult(PayoutOutcome.Unknown, null, "SIMULATED_TIMEOUT", "Giả lập timeout, lệnh chưa được ghi nhận."));
            if (balance < request.Amount)
                return Task.FromResult(new PayoutResult(PayoutOutcome.DefiniteFailure, null, "INSUFFICIENT_BALANCE", "Số dư tài khoản chi không đủ."));

            balance -= request.Amount;
            var entry = new Entry
            {
                Request = request, ProviderRef = "SIM-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                CompleteAt = clock.GetUtcNow() + options.Delay, WillFail = failure == SimulatedFailure.FailAfterAccept,
            };
            byKey[request.IdempotencyKey] = entry;
            byReference[request.ReferenceId] = entry;
            return Task.FromResult(failure == SimulatedFailure.UnknownRecorded
                ? new PayoutResult(PayoutOutcome.Unknown, null, "SIMULATED_TIMEOUT", "Giả lập timeout, lệnh đã được ghi nhận.")
                : new PayoutResult(PayoutOutcome.Accepted, entry.ProviderRef));
        }
    }

    public Task<PayoutStatusResult> GetPayoutAsync(string referenceOrProviderRef, CancellationToken ct = default)
    {
        lock (gate)
        {
            var entry = byReference.GetValueOrDefault(referenceOrProviderRef) ?? byReference.Values.FirstOrDefault(e => e.ProviderRef == referenceOrProviderRef);
            if (entry == null) return Task.FromResult(new PayoutStatusResult(PayoutStatus.NotFound, null));
            if (clock.GetUtcNow() < entry.CompleteAt) return Task.FromResult(new PayoutStatusResult(PayoutStatus.Processing, entry.ProviderRef));
            if (!entry.WillFail) return Task.FromResult(new PayoutStatusResult(PayoutStatus.Succeeded, entry.ProviderRef));
            if (!entry.Refunded) { entry.Refunded = true; balance += entry.Request.Amount; }
            return Task.FromResult(new PayoutStatusResult(PayoutStatus.Failed, entry.ProviderRef, "Giả lập chuyển thất bại."));
        }
    }

    public Task<decimal> GetBalanceAsync(CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult(balance);
    }

    private static string? Validate(PayoutRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ReferenceId)) return "Thiếu referenceId.";
        if (string.IsNullOrWhiteSpace(r.IdempotencyKey)) return "Thiếu khóa idempotency.";
        if (r.Amount <= 0 || r.Amount != decimal.Truncate(r.Amount)) return "Số tiền phải là số nguyên đồng lớn hơn 0.";
        if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Length > 20) return "Mô tả bắt buộc và tối đa 20 ký tự.";
        if (r.ToBin is not { Length: 6 } || !r.ToBin.All(char.IsAsciiDigit)) return "Mã BIN phải gồm đúng 6 chữ số.";
        if (string.IsNullOrWhiteSpace(r.ToAccountNumber)) return "Thiếu số tài khoản nhận.";
        return null;
    }
}
