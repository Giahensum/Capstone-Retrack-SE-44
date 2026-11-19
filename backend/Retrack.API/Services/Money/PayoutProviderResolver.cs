namespace Retrack.API.Services.Money;

/// <summary>Chọn nhà cung cấp chi theo cấu hình hệ thống payout.provider.</summary>
public interface IPayoutProviderResolver
{
    /// <summary>Nhà cung cấp đang được chọn. PAYOS mà thiếu khóa kênh chi thì ném lỗi tiếng Việt, KHÔNG lặng lẽ chuyển sang mô phỏng.</summary>
    Task<IPayoutProvider> ResolveCurrentAsync(CancellationToken ct = default);
    /// <summary>Nhà cung cấp theo tên đã lưu trên kỳ chi (SIMULATED hoặc PAYOS) để đối soát/gửi lại đúng kênh đã gửi.</summary>
    IPayoutProvider GetByName(string name);
}

public sealed class PayoutProviderResolver(MoneyConfigReader config, SimulatedPayoutProvider simulated, PayOsPayoutProvider payOs) : IPayoutProviderResolver
{
    public async Task<IPayoutProvider> ResolveCurrentAsync(CancellationToken ct = default) =>
        GetByName((await config.GetAsync(ct)).PayoutProvider);

    public IPayoutProvider GetByName(string name)
    {
        var provider = string.Equals(name, "PAYOS", StringComparison.OrdinalIgnoreCase) ? (IPayoutProvider)payOs : simulated;
        if (!provider.IsConfigured)
            throw new InvalidOperationException("Hệ thống được cấu hình chi qua payOS nhưng chưa có khóa kênh chi (PayOSPayout__ClientId, __ApiKey, __ChecksumKey). Không tự chuyển sang mô phỏng.");
        return provider;
    }
}
