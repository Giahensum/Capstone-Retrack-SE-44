using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.API.Services.Factory;

// Tệp Factory nằm ngoài database; URL được lưu trong các cột chứng từ hiện có.
public sealed class FactoryAttachmentService(AppDbContext db, IWebHostEnvironment environment)
{
    private const int MaxBytes = 700 * 1024;
    private readonly string root = Path.Combine(environment.ContentRootPath, "uploads", "factory");

    public async Task<string> UploadAsync(Guid ownerId, IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaxBytes)
            throw new ArgumentException("Tệp phải có dung lượng từ 1 byte đến 700 KB.");
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        if (buffer.Length != file.Length || buffer.Length > MaxBytes)
            throw new ArgumentException("Dung lượng tệp không hợp lệ.");
        var bytes = buffer.ToArray();
        var extension = DetectExtension(bytes);
        if (extension is null)
            throw new ArgumentException("Chỉ nhận tệp PDF, JPG hoặc PNG hợp lệ.");

        var directory = Path.Combine(root, ownerId.ToString("N"));
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid();
        var filePath = Path.Combine(directory, $"{id:N}.{extension}");
        await File.WriteAllBytesAsync(filePath, bytes, ct);
        return Url(ownerId, id);
    }

    public async Task<(string Path, string ContentType)> OpenAsync(Guid actorId, string role, Guid ownerId, Guid id, CancellationToken ct)
    {
        var url = Url(ownerId, id);
        var allowed = role == "ADMIN" ||
            (role == "FACTORY" && actorId == ownerId) ||
            (role == "DEPOT_OWNER" && await db.InventoryBatches.AsNoTracking().AnyAsync(
                batch => batch.Depot.OwnerId == actorId && batch.TargetFactory != null && batch.TargetFactory.OwnerId == ownerId &&
                    batch.QualityCheck != null && (batch.QualityCheck.TicketImageUrl == url || batch.QualityCheck.InvoiceFileUrl == url), ct));
        if (!allowed) throw new UnauthorizedAccessException("Không có quyền đọc tệp này.");

        foreach (var (extension, contentType) in new[] { ("pdf", "application/pdf"), ("jpg", "image/jpeg"), ("png", "image/png") })
        {
            var filePath = Path.Combine(root, ownerId.ToString("N"), $"{id:N}.{extension}");
            if (File.Exists(filePath)) return (filePath, contentType);
        }
        throw new KeyNotFoundException("Không tìm thấy tệp đính kèm.");
    }

    public bool IsOwnedUrl(string? url, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var prefix = $"/api/factory/attachments/{ownerId:N}/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return Guid.TryParseExact(url[prefix.Length..], "N", out var id) &&
            new[] { "pdf", "jpg", "png" }.Any(ext => File.Exists(Path.Combine(root, ownerId.ToString("N"), $"{id:N}.{ext}")));
    }

    private static string Url(Guid ownerId, Guid id) => $"/api/factory/attachments/{ownerId:N}/{id:N}";

    private static string? DetectExtension(byte[] data)
    {
        if (data.Length >= 5 && data.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) return "pdf";
        if (data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff) return "jpg";
        if (data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        return null;
    }
}
