namespace Retrack.API.Services.Interfaces;

public interface ICloudinaryService
{
    Task<string> UploadImageAsync(Stream fileStream, string fileName);

    Task<string> UploadAvatarAsync(Stream fileStream, string fileName);

    Task<bool> DeleteImageAsync(string publicId);
}


