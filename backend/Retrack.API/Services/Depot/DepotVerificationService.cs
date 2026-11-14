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
    public async Task<Guid> SubmitAsync(Guid ownerId, Guid depotId, Guid operationId, string? note, IReadOnlyList<IFormFile> files, IReadOnlyList<string>? fileKinds = null)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (operationId == Guid.Empty || files.Count is < 1 or > 3 || note?.Length > 2000) throw new ArgumentException("Cần 1–3 tệp hồ sơ và ghi chú tối đa 2000 ký tự.");
        // Client cũ chưa phân loại vẫn được giữ nguyên contract; giao diện mới phải có giấy tờ kinh doanh.
        if (fileKinds != null && (fileKinds.Count != files.Count || !fileKinds.Contains("REGISTRATION") ||
            fileKinds.Any(kind => kind is not ("REGISTRATION" or "DEPOT_PHOTO" or "SUPPORTING"))))
            throw new ArgumentException("Cần ít nhất một giấy tờ kinh doanh và phân loại đúng từng tệp.");
        var documents = new List<DepotVerificationDocument>();
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var kind = fileKinds?[index] ?? "LEGACY";
            if (file.Length is <= 0 or > 2 * 1024 * 1024) throw new ArgumentException("Mỗi tệp tối đa 2 MB, không nhận tệp rỗng.");
            await using var input = file.OpenReadStream();
            // Đọc tối đa kích thước khai báo + 1, kể cả khi stream khai báo sai Length.
            var buffer = new byte[(int)file.Length + 1];
            var count = await input.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false);
            var content = buffer.AsSpan(0, count).ToArray();
            var type = ContentType(content);
            if (content.Length != file.Length || content.Length > 2 * 1024 * 1024 || type == null) throw new ArgumentException("Chỉ nhận nội dung PDF, PNG hoặc JPEG hợp lệ, tối đa 2 MB.");
            if (kind == "DEPOT_PHOTO" && type == "application/pdf") throw new ArgumentException("Ảnh kho cần là PNG hoặc JPEG; không dùng PDF.");
            var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || name.Any(char.IsControl)) throw new ArgumentException("Tên tệp không hợp lệ, tối đa 150 ký tự.");
            var extension = Path.GetExtension(name).ToLowerInvariant();
            if ((type == "application/pdf" && extension != ".pdf") ||
                (type == "image/png" && extension != ".png") ||
                (type == "image/jpeg" && extension is not (".jpg" or ".jpeg")))
                throw new ArgumentException("Đuôi tệp phải khớp nội dung PDF, PNG hoặc JPEG.");
            documents.Add(new() { Name = name, ContentType = type, Content = content, Kind = kind });
        }
        await using var tx = await work.BeginAsync(); await repository.LockDepotAsync(depotId);
        var old = await repository.FindAsync(operationId);
        if (old != null)
        {
            if (old.DepotId != depotId) throw new DepotForbiddenException();
            old = await repository.FindAsync(operationId, includeDocuments: true) ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ.");
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
        repository.AddNotice(Notification.ForDepot(depot.OwnerId, depot.Id, "VERIFICATION",
            dto.Approve ? "Hồ sơ kho đã được duyệt" : "Hồ sơ kho cần bổ sung",
            $"Kho {depot.Name}: {dto.Reason.Trim()}", targetType: "PROFILE"));
        await work.SaveAsync(); await tx.CommitAsync();
    }
    public async Task<DepotVerificationDocument> FileAsync(Guid actor, Guid id)
    {
        var owner = await repository.FileOwnerAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy tệp.");
        if (owner != actor) await RequireAdmin(actor);
        else await scope.GetDepotsAsync(actor);
        return await repository.FileAsync(id, actor) ?? throw new DepotForbiddenException();
    }
    private static string? ContentType(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (bytes.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10})) return "image/png";
        if (bytes.AsSpan().StartsWith(new byte[] {255,216,255})) return "image/jpeg";
        return null;
    }
    private static IEnumerable<string> Fingerprints(IEnumerable<DepotVerificationDocument> files) => files
        .Select(x => x.Kind + ":" + x.Name + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(x.Content))).OrderBy(x => x, StringComparer.Ordinal);
}
