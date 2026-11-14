using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class VerificationScannerOptions
{
    public string Mode { get; set; } = "Disabled";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class VerificationScannerUnavailableException : Exception
{
    public VerificationScannerUnavailableException() : base("Dịch vụ kiểm tra an toàn tệp chưa sẵn sàng. Hồ sơ chưa được nộp; vui lòng thử lại sau.") { }
}

public sealed class VerificationFileScanner(IOptions<VerificationScannerOptions> options, IHostEnvironment environment)
    : IVerificationFileScanner, IDisposable
{
    private readonly VerificationScannerOptions config = options.Value;
    private readonly SemaphoreSlim capacity = new(2, 2);
    public VerificationScanPolicy Policy => config.Mode == "ClamAv"
        ? new(true, "ClamAv", "Tệp mới được quét trước khi lưu. Nếu dịch vụ quét gián đoạn, hồ sơ sẽ chưa được nhận.")
        : environment.IsDevelopment() && config.Mode == "Disabled"
            ? new(true, "DevelopmentOnly", "Môi trường phát triển chưa bật quét mã độc. Chỉ dùng tài liệu kiểm thử, không nộp giấy tờ thật.")
            : new(false, "Unavailable", "Chưa cấu hình dịch vụ kiểm tra an toàn tệp. Liên hệ quản trị để mở chức năng nộp hồ sơ.");

    public async Task<bool> ScanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content.Length is <= 0 or > 2 * 1024 * 1024) throw new ArgumentException("Tệp cần nằm trong giới hạn 2 MB.");
        if (Policy.Mode == "DevelopmentOnly") return false;
        if (config.Mode != "ClamAv") throw new VerificationScannerUnavailableException();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        var acquired = false;
        try
        {
            await capacity.WaitAsync(timeout.Token); acquired = true;
            using var client = new TcpClient();
            await client.ConnectAsync(config.Host, config.Port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            var length = new byte[4];
            for (var offset = 0; offset < content.Length; offset += 64 * 1024)
            {
                var chunk = content.Slice(offset, Math.Min(64 * 1024, content.Length - offset));
                BinaryPrimitives.WriteInt32BigEndian(length, chunk.Length);
                await stream.WriteAsync(length, timeout.Token);
                await stream.WriteAsync(chunk, timeout.Token);
            }
            await stream.WriteAsync(new byte[4], timeout.Token);
            // Phản hồi không đáng tin: giới hạn 1024 byte, bắt buộc kết thúc bằng NUL.
            var reply = new byte[1024]; var count = 0;
            while (count < reply.Length)
            {
                var read = await stream.ReadAsync(reply.AsMemory(count, 1), timeout.Token);
                if (read == 0) throw new VerificationScannerUnavailableException();
                if (reply[count] == 0)
                {
                    var result = Encoding.UTF8.GetString(reply, 0, count);
                    if (result == "stream: OK") return true;
                    if (result.StartsWith("stream: ", StringComparison.Ordinal) && result.EndsWith(" FOUND", StringComparison.Ordinal))
                        throw new ArgumentException("Tệp có dấu hiệu không an toàn và chưa được lưu. Hãy kiểm tra lại tài liệu.");
                    throw new VerificationScannerUnavailableException();
                }
                count += read;
            }
            throw new VerificationScannerUnavailableException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new VerificationScannerUnavailableException(); }
        catch (Exception error) when (error is SocketException or IOException) { throw new VerificationScannerUnavailableException(); }
        finally { if (acquired) capacity.Release(); }
    }
    public void Dispose() => capacity.Dispose();
}
