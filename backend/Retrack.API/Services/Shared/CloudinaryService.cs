using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Shared;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        var cloudName = config["Cloudinary:CloudName"];
        var apiKey = config["Cloudinary:ApiKey"];
        var apiSecret = config["Cloudinary:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            throw new InvalidOperationException("Chưa cấu hình dịch vụ ảnh. Vui lòng liên hệ quản trị viên.");

        _cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret)) { Api = { Secure = true } };
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName)
    {
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = "retrack",
            Transformation = new Transformation()
                .Width(1200).Height(1200).Crop("limit")
                .Quality("auto").FetchFormat("auto")
        });

        if (result.StatusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException($"Không thể tải ảnh lên. Vui lòng thử lại sau. ({result.Error?.Message})");

        return result.SecureUrl.ToString();
    }

    public async Task<string> UploadAvatarAsync(Stream fileStream, string fileName)
    {
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
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
        (await _cloudinary.DestroyAsync(new DeletionParams(publicId))).Result == "ok";
}
