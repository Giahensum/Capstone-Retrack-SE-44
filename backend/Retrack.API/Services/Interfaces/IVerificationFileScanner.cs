using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Interfaces;

public interface IVerificationFileScanner
{
    VerificationScanPolicy Policy { get; }
    // true: daemon trả kết quả sạch; false: chỉ được bỏ quét ở Development.
    Task<bool> ScanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
}
