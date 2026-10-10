using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Shared;

public class CloudinaryService : ICloudinaryService
{
    private readonly IConfiguration _config;
    private readonly ILogger<CloudinaryService> _logger;
    private Cloudinary? _cloudinary;

    public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private Cloudinary Client
    {
        get
        {
            if (_cloudinary != null) return _cloudinary;
            var cloudName = _config["Cloudinary:CloudName"];
            var apiKey = _config["Cloudinary:ApiKey"];
            var apiSecret = _config["Cloudinary:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
                throw new InvalidOperationException("Chưa cấu hình dịch vụ ảnh. Vui lòng liên hệ quản trị viên.");

            return _cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret)) { Api = { Secure = true } };
        }
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName)
    {
        // Không coi ảnh mẫu của nhà cung cấp là chứng từ/ảnh vật liệu của người dùng.
        var client = Client;
        try
        {
            var result = await client.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = "retrack",
                Transformation = new Transformation()
                    .Width(1200).Height(1200).Crop("limit")
                    .Quality("auto").FetchFormat("auto")
            });

            if (result.Error == null && result.StatusCode == System.Net.HttpStatusCode.OK && result.SecureUrl != null)
                return result.SecureUrl.ToString();
            _logger.LogError("Cloudinary image upload returned failure for file {FileName}: {Error}", Path.GetFileName(fileName), result.Error?.Message ?? $"HTTP {(int)result.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cloudinary image upload failed for file {FileName}", Path.GetFileName(fileName));
            throw new InvalidOperationException("Không thể tải ảnh lên. Vui lòng kiểm tra cấu hình dịch vụ ảnh hoặc thử lại sau.", ex);
        }
        throw new InvalidOperationException("Không thể tải ảnh lên. Vui lòng kiểm tra cấu hình dịch vụ ảnh hoặc thử lại sau.");
    }

    public async Task<string> UploadAvatarAsync(Stream fileStream, string fileName)
    {
        var result = await Client.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = "retrack/avatars",
            Transformation = new Transformation()
                .Width(512).Height(512).Crop("fill")
                .Quality("auto").FetchFormat("jpg")
        });

        if (result.Error != null || result.SecureUrl == null)
            throw new InvalidOperationException("Không thể tải ảnh lên. Vui lòng thử lại sau.");

        return result.SecureUrl.ToString();
    }

    public async Task<bool> DeleteImageAsync(string publicId) =>
        (await Client.DestroyAsync(new DeletionParams(publicId))).Result == "ok";
}
