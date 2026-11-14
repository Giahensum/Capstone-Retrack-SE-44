using System.Net;
using System.Text.RegularExpressions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Shared;

public sealed class VerificationStorageOptions
{
    public string Provider { get; set; } = "Database";
}

public sealed class VerificationStorageUnavailableException : Exception
{
    public VerificationStorageUnavailableException() : base("Kho lưu hồ sơ đang gián đoạn. Vui lòng thử lại sau.") { }
}

public sealed class CloudinaryVerificationStorage : IVerificationDocumentStorage
{
    public const string HttpClientName = "DepotPrivateVerification";
    public const string Prefix = "retrack/private-depot-verifications/";
    private readonly Lazy<Cloudinary> cloud;
    private readonly IHttpClientFactory clients;
    private readonly ILogger<CloudinaryVerificationStorage> logger;
    private readonly VerificationStorageOptions options;
    public string Provider => options.Provider.ToUpperInvariant();

    public CloudinaryVerificationStorage(IConfiguration config, IOptions<VerificationStorageOptions> settings,
        IHttpClientFactory clients, ILogger<CloudinaryVerificationStorage> logger)
    {
        options = settings.Value; this.clients = clients; this.logger = logger;
        cloud = new(() =>
        {
            var name = config["Cloudinary:CloudName"]; var key = config["Cloudinary:ApiKey"]; var secret = config["Cloudinary:ApiSecret"];
            if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[A-Za-z0-9_-]{1,80}$") ||
                string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret)) throw new VerificationStorageUnavailableException();
            return new Cloudinary(new Account(name, key, secret)) { Api = { Secure = true } };
        });
    }

    public static bool ValidKey(string key) => Regex.IsMatch(key,
        "^retrack/private-depot-verifications/[a-f0-9]{32}/[a-f0-9]{32}\\.(pdf|png|jpg)$", RegexOptions.CultureInvariant);
    public Uri DownloadUri(string key)
    {
        if (!ValidKey(key)) throw new VerificationStorageUnavailableException();
        var uri = new Uri(cloud.Value.DownloadPrivate(key, attachment: true, type: "authenticated",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(), resourceType: "raw"));
        if (uri.Scheme != "https" || uri.Host != "api.cloudinary.com") throw new VerificationStorageUnavailableException();
        return uri;
    }
    public async Task<string> UploadAsync(Guid operationId, Guid documentId, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        var extension = contentType switch { "application/pdf" => "pdf", "image/png" => "png", "image/jpeg" => "jpg", _ => throw new ArgumentException("Loại tệp không hợp lệ.") };
        if (operationId == Guid.Empty || documentId == Guid.Empty || content.Length is < 1 or > 2097152) throw new ArgumentException("Tệp không hợp lệ.");
        var key = $"{Prefix}{operationId:N}/{documentId:N}.{extension}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await using var stream = new MemoryStream(content, writable: false);
            var result = await cloud.Value.UploadAsync(new RawUploadParams
            {
                File = new FileDescription(documentId.ToString("N") + "." + extension, stream),
                PublicId = key, Type = "authenticated", Overwrite = false, UseFilename = false, UniqueFilename = false,
            }, "raw", timeout.Token);
            if (result.Error != null || result.StatusCode != HttpStatusCode.OK || result.PublicId != key ||
                result.Type != "authenticated" || result.ResourceType != "raw" || result.Bytes != content.Length)
                throw new VerificationStorageUnavailableException();
            return key;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            // Chỉ ghi UUID để đối soát; không log URL ký, tên giấy tờ, credential hoặc lỗi provider thô.
            logger.LogWarning("Chưa xác nhận upload hồ sơ cloud {DocumentId}; cần đối soát nếu request đã tới provider.", documentId);
            throw new VerificationStorageUnavailableException();
        }
    }
    public async Task<byte[]> ReadAsync(string key, int expectedSize, CancellationToken cancellationToken)
    {
        if (expectedSize is < 1 or > 2097152) throw new VerificationStorageUnavailableException();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            // Client riêng đã bỏ logger URL, không chuyển tiếp redirect hoặc URL ký cho browser.
            using var client = clients.CreateClient(HttpClientName);
            using var response = await client.GetAsync(DownloadUri(key), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode || (response.Content.Headers.ContentLength is long length && length != expectedSize))
                throw new VerificationStorageUnavailableException();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new byte[expectedSize + 1];
            var count = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, timeout.Token);
            if (count != expectedSize) throw new VerificationStorageUnavailableException();
            return buffer.AsSpan(0, count).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { throw new VerificationStorageUnavailableException(); }
    }
    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (!ValidKey(key)) throw new VerificationStorageUnavailableException();
        try
        {
            var result = await cloud.Value.DestroyAsync(new DeletionParams(key) { ResourceType = ResourceType.Raw, Type = "authenticated", Invalidate = true }).WaitAsync(cancellationToken);
            if (result.Result is not ("ok" or "not found")) throw new VerificationStorageUnavailableException();
        }
        catch
        {
            logger.LogWarning("Chưa dọn được tài liệu cloud {DocumentId}; cần đối soát tài nguyên không có tham chiếu.", Path.GetFileNameWithoutExtension(key));
            throw new VerificationStorageUnavailableException();
        }
    }
}
