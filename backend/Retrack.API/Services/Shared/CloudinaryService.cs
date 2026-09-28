using Retrack.API.Services.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
namespace Retrack.API.Services.Shared;

public class CloudinaryService(IConfiguration configuration) : ICloudinaryService
{
    private Cloudinary CreateClient()
    {
        var cloud = configuration["Cloudinary:CloudName"];
        var key = configuration["Cloudinary:ApiKey"];
        var secret = configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloud) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Chưa cấu hình dịch vụ ảnh. Vui lòng liên hệ quản trị viên.");
        return new Cloudinary(new Account(cloud, key, secret)) { Api = { Secure = true } };
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName)
    {
        var result = await CreateClient().UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = "retrack/avatars",
            Transformation = new Transformation().Width(512).Height(512).Crop("fill").Quality("auto").FetchFormat("jpg")
        });
        if (result.Error != null || result.SecureUrl == null)
            throw new InvalidOperationException("Không thể tải ảnh lên. Vui lòng thử lại sau.");
        return result.SecureUrl.ToString();
    }

    public async Task<bool> DeleteImageAsync(string publicId) =>
        (await CreateClient().DestroyAsync(new DeletionParams(publicId))).Result == "ok";
}


