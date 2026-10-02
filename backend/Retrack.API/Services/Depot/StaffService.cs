using System.ComponentModel.DataAnnotations;
using System.Text;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class StaffService(IDepotStaffRepository repository, IDepotService scope) : IStaffService
{
    public async Task<PagedResult<StaffDto>> ListAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!string.IsNullOrEmpty(query.Status) && query.Status is not ("ACTIVE" or "INACTIVE"))
            throw new ArgumentException("Trạng thái nhân viên không hợp lệ.");
        return await repository.ListAsync(depotId, query);
    }
    public async Task<StaffDto> CreateAsync(Guid ownerId, Guid depotId, CreateStaffDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        if (Encoding.UTF8.GetByteCount(dto.Password) > 72) throw new ArgumentException("Mật khẩu vượt quá 72 byte UTF-8.");
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await repository.EmailExistsAsync(email)) throw new DepotConflictException("Email đã được sử dụng.");
        var user = new User { FullName = dto.FullName.Trim(), Email = email, Phone = dto.Phone.Trim(),
            Role = dto.Role, PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password) };
        var staff = new DepotStaff { DepotId = depotId, User = user, StaffType = dto.Role };
        if (!await repository.TryCreateAsync(staff)) throw new DepotConflictException("Email đã được sử dụng.");
        return new(staff.Id, user.Id, user.FullName, user.Email, user.Phone, staff.StaffType, staff.IsActive);
    }
    public async Task<StaffDto> UpdateAsync(Guid ownerId, Guid depotId, Guid staffId, UpdateStaffDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        var staff = await repository.FindAsync(depotId, staffId)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên trong kho.");
        staff.User.FullName = dto.FullName.Trim(); staff.User.Phone = dto.Phone.Trim();
        staff.User.IsActive = dto.IsActive; staff.IsActive = dto.IsActive; staff.User.UpdatedAt = DateTime.UtcNow;
        await repository.SaveAsync();
        return new(staff.Id, staff.UserId, staff.User.FullName, staff.User.Email, staff.User.Phone, staff.StaffType, staff.IsActive);
    }
}
