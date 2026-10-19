using System.ComponentModel.DataAnnotations;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Services.Depot;

public sealed class DepotVerificationService(IDepotVerificationRepository repository, IDepotUnitOfWork work, IDepotService scope)
{
    public async Task<DepotProfileDto> CreateDepotAsync(Guid ownerId, CreateDepotDto dto)
    {
        await scope.GetDepotsAsync(ownerId);
        if (dto.OperationId == Guid.Empty || dto.Profile == null) throw new ArgumentException("Thiếu thông tin kho hoặc mã thao tác.");
        Validator.ValidateObject(dto.Profile, new ValidationContext(dto.Profile), true);
        if (string.IsNullOrWhiteSpace(dto.Profile.Name) || string.IsNullOrWhiteSpace(dto.Profile.Address) || dto.Profile.Address.Length > 1000)
            throw new ArgumentException("Cần tên kho và địa chỉ hợp lệ.");
        await using var tx = await work.BeginAsync(); await repository.LockOwnerAsync(ownerId);
        var depot = await repository.FindDepotAsync(dto.OperationId);
        if (depot != null)
        {
            if (depot.OwnerId != ownerId) throw new DepotForbiddenException();
            if (depot.Name != dto.Profile.Name.Trim() || depot.Address != dto.Profile.Address.Trim()) throw new DepotConflictException("Mã thao tác đã dùng cho kho khác.");
        }
        else
        {
            depot = new Retrack.API.Models.Depot { Id = dto.OperationId, OwnerId = ownerId, Name = dto.Profile.Name.Trim(), Address = dto.Profile.Address.Trim(),
                TaxCode = dto.Profile.TaxCode, ContactPhone = dto.Profile.ContactPhone, Description = dto.Profile.Description,
                Latitude = dto.Profile.Latitude, Longitude = dto.Profile.Longitude };
            repository.AddDepot(depot); await work.SaveAsync();
        }
        await tx.CommitAsync(); return await scope.GetProfileAsync(ownerId, depot.Id);
    }
    private async Task RequireAdmin(Guid id) { if (!await repository.ActiveAdminAsync(id)) throw new DepotForbiddenException(); }
    public async Task<PagedResult<VerificationSubmissionDto>> ListAsync(Guid actor, Guid? depotId, DepotQuery query)
    {
        if (depotId.HasValue) await scope.RequireOwnerAsync(actor, depotId.Value); else await RequireAdmin(actor);
        if (query.Status is not (null or "" or "SUBMITTED" or "APPROVED" or "REJECTED")) throw new ArgumentException("Trạng thái hồ sơ không hợp lệ.");
        return await repository.ListAsync(depotId, query);
    }
    public async Task<Guid> SubmitAsync(Guid ownerId, Guid depotId, Guid operationId, string? note, IReadOnlyList<IFormFile> files)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (operationId == Guid.Empty || files.Count is < 1 or > 3 || note?.Length > 2000) throw new ArgumentException("Cần 1–3 tệp hồ sơ và ghi chú tối đa 2000 ký tự.");
        var documents = new List<DepotVerificationDocument>();
        foreach (var file in files)
        {
            if (file.Length is <= 0 or > 2 * 1024 * 1024) throw new ArgumentException("Mỗi tệp tối đa 2 MB, không nhận tệp rỗng.");
            await using var input = file.OpenReadStream(); using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer);
            var content = buffer.ToArray();
            var type = ContentType(content);
            if (content.Length != file.Length || content.Length > 2 * 1024 * 1024 || type == null) throw new ArgumentException("Chỉ nhận nội dung PDF, PNG hoặc JPEG hợp lệ, tối đa 2 MB.");
            var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || name.Any(char.IsControl)) throw new ArgumentException("Tên tệp không hợp lệ, tối đa 150 ký tự.");
            documents.Add(new() { Name = name, ContentType = type, Content = content });
        }
        await using var tx = await work.BeginAsync(); await repository.LockDepotAsync(depotId);
        var old = await repository.FindAsync(operationId);
        if (old != null)
        {
            if (old.DepotId != depotId) throw new DepotForbiddenException();
            if (old.Note != (note?.Trim() ?? "") || !Fingerprints(old.Documents).SequenceEqual(Fingerprints(documents)))
                throw new DepotConflictException("Mã thao tác đã dùng cho hồ sơ khác.");
            await tx.CommitAsync(); return old.Id;
        }
        if (await repository.HasPendingAsync(depotId)) throw new DepotConflictException("Kho đã có hồ sơ chờ duyệt.");
        if (await repository.RecentCountAsync(depotId) >= 5) throw new DepotConflictException("Tối đa 5 lần nộp hồ sơ mỗi ngày.");
        var depot = await repository.FindDepotAsync(depotId) ?? throw new KeyNotFoundException("Không tìm thấy kho.");
        if (depot.VerificationStatus == "APPROVED") throw new DepotConflictException("Kho đã xác minh; không nộp hồ sơ trùng.");
        repository.Add(new() { Id = operationId, DepotId = depotId, Note = note?.Trim() ?? "", Documents = documents,
            DepotName = depot.Name, DepotAddress = depot.Address, TaxCode = depot.TaxCode });
        depot.VerificationStatus = "SUBMITTED";
        await work.SaveAsync(); await tx.CommitAsync(); return operationId;
    }
    public async Task ReviewAsync(Guid adminId, Guid id, VerificationReviewDto dto)
    {
        await RequireAdmin(adminId);
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 2000) throw new ArgumentException("Cần lý do/kết quả đối soát, tối đa 2000 ký tự.");
        var submission = await repository.FindAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ.");
        await using var tx = await work.BeginAsync(); await repository.LockDepotAsync(submission.DepotId);
        // Đọc lại sau khóa kho để hai Admin không ghi đè quyết định của nhau.
        submission = await repository.FindAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ.");
        var status = dto.Approve ? "APPROVED" : "REJECTED";
        if (submission.Status != "SUBMITTED")
        {
            if (submission.Status == status && submission.ReviewReason == dto.Reason.Trim()) { await tx.CommitAsync(); return; }
            throw new DepotConflictException("Hồ sơ đã được xử lý. Hãy tải lại.");
        }
        var depot = await repository.FindDepotAsync(submission.DepotId) ?? throw new KeyNotFoundException("Không tìm thấy kho.");
        if (dto.Approve && (depot.Name != submission.DepotName || depot.Address != submission.DepotAddress || depot.TaxCode != submission.TaxCode))
            throw new DepotConflictException("Thông tin kho đã thay đổi so với hồ sơ. Hãy yêu cầu bổ sung thay vì duyệt hồ sơ cũ.");
        submission.Status = status; submission.ReviewedAt = DateTime.UtcNow; submission.ReviewedBy = adminId; submission.ReviewReason = dto.Reason.Trim();
        depot.VerificationStatus = status;
        await work.SaveAsync(); await tx.CommitAsync();
    }
    public async Task<DepotVerificationDocument> FileAsync(Guid actor, Guid id)
    {
        var row = await repository.FileAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy tệp.");
        if (row.OwnerId != actor) await RequireAdmin(actor);
        else await scope.GetDepotsAsync(actor);
        return row.Document;
    }
    private static string? ContentType(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (bytes.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10})) return "image/png";
        if (bytes.AsSpan().StartsWith(new byte[] {255,216,255})) return "image/jpeg";
        return null;
    }
    private static IEnumerable<string> Fingerprints(IEnumerable<DepotVerificationDocument> files) => files
        .Select(x => x.Name + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(x.Content))).OrderBy(x => x, StringComparer.Ordinal);
}
