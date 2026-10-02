using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class DepotProofService(IDepotService scope, ICloudinaryService images) : IDepotProofService
{
    public async Task<string> UploadAsync(Guid ownerId, Guid depotId, IFormFile file)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        const int limit = 10 * 1024 * 1024;
        if (file is null || file.Length <= 0 || file.Length > limit)
            throw new ArgumentException("Ảnh chứng từ phải có dung lượng từ 1 byte đến 10 MB.");
        using var buffer = new MemoryStream();
        using var source = file.OpenReadStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + count > limit) throw new ArgumentException("Ảnh chứng từ vượt quá 10 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count));
        }
        var bytes = buffer.ToArray();
        var extension = file.ContentType switch
        {
            "image/png" when bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => ".png",
            "image/jpeg" when bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) => ".jpg",
            "image/webp" when bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) => ".webp",
            _ => throw new ArgumentException("Chỉ chấp nhận ảnh PNG, JPEG hoặc WebP đúng định dạng.")
        };
        buffer.Position = 0;
        return await images.UploadImageAsync(buffer, $"depot-proof-{Guid.NewGuid():N}{extension}");
    }
}
