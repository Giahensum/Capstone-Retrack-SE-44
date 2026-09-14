using Retrack.API.Services.Interfaces;
namespace Retrack.API.Services.Shared;

public class CloudinaryService : ICloudinaryService
{
    // TODO: Implement Cloudinary upload
    public async Task<string> UploadImageAsync(Stream fileStream, string fileName) => throw new NotImplementedException();
    public async Task<bool> DeleteImageAsync(string publicId) => throw new NotImplementedException();
}
