using Retrack.API.DTOs.Staff;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Staff;

public interface IStaffProfileService
{
    Task<StaffProfileResponse> GetAsync(Guid userId, string role, CancellationToken ct);
    Task<StaffProfileResponse> UpdateAsync(Guid userId, string role, UpdateStaffProfileRequest request, CancellationToken ct);
    Task<StaffProfileResponse> UploadAvatarAsync(Guid userId, string role, IFormFile file, CancellationToken ct);
}

public sealed class StaffProfileService(IStaffProfileRepository repository, ICloudinaryService images) : IStaffProfileService
{
    private async Task<DepotStaff> RequireStaff(Guid userId, string role, CancellationToken ct)
    {
        if (role is not ("DEPOT_EMPLOYEE" or "DRIVER"))
            throw new UnauthorizedAccessException("Vai trò không được sử dụng ứng dụng nhân sự.");
        return await repository.FindActiveAsync(userId, role, ct)
            ?? throw new UnauthorizedAccessException("Tài khoản hoặc liên kết kho không còn hoạt động. Vui lòng liên hệ chủ kho.");
    }

    public async Task<StaffProfileResponse> GetAsync(Guid userId, string role, CancellationToken ct) =>
        Map(await RequireStaff(userId, role, ct));

    public async Task<StaffProfileResponse> UpdateAsync(Guid userId, string role, UpdateStaffProfileRequest request, CancellationToken ct)
    {
        var staff = await RequireStaff(userId, role, ct);
        staff.User.Phone = request.Phone;
        staff.User.UpdatedAt = DateTime.UtcNow;
        await repository.SaveAsync(ct);
        return Map(staff);
    }

    public async Task<StaffProfileResponse> UploadAvatarAsync(Guid userId, string role, IFormFile file, CancellationToken ct)
    {
        var staff = await RequireStaff(userId, role, ct);
        if (file.Length is <= 0 or > 5 * 1024 * 1024)
            throw new ArgumentException("Ảnh phải có dung lượng từ 1 byte đến 5 MB.");
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAsync(header, ct);
        var png = read == 8 && header.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        var jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (!png && !jpeg)
            throw new ArgumentException("Chỉ chấp nhận ảnh JPEG hoặc PNG.");
        await using var upload = new MemoryStream();
        await upload.WriteAsync(header.AsMemory(0, read), ct);
        await stream.CopyToAsync(upload, ct);
        upload.Position = 0;
        staff.User.AvatarUrl = await images.UploadImageAsync(upload, $"avatar-{Guid.NewGuid():N}.{(png ? "png" : "jpg")}");
        staff.User.UpdatedAt = DateTime.UtcNow;
        await repository.SaveAsync(ct);
        return Map(staff);
    }

    private static StaffProfileResponse Map(DepotStaff staff) => new(
        staff.UserId, staff.User.FullName, staff.User.Email, staff.User.Phone, staff.User.Role,
        staff.User.AvatarUrl, staff.Id, $"{(staff.StaffType == "DRIVER" ? "DRV" : "EMP")}-{staff.Id.ToString("N")[..8].ToUpperInvariant()}",
        staff.DepotId, staff.Depot.Name, staff.Depot.Address, staff.StaffType, staff.IsActive);
}
