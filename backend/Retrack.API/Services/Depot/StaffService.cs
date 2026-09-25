using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class StaffService(AppDbContext db, IDepotService scope) : IStaffService
{
    public async Task<PagedResult<StaffDto>> ListAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var source = db.DepotStaffs.AsNoTracking().Where(s => s.DepotId == depotId);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(s => s.User.FullName.Contains(query.Search) || s.User.Email.Contains(query.Search));
        if (!string.IsNullOrEmpty(query.Status))
        {
            if (query.Status is not ("ACTIVE" or "INACTIVE")) throw new ArgumentException("Trạng thái nhân viên không hợp lệ.");
            source = source.Where(s => s.IsActive == (query.Status == "ACTIVE"));
        }
        return await DepotService.PageAsync(source.OrderBy(s => s.User.FullName).ThenBy(s => s.Id)
            .Select(s => new StaffDto(s.Id, s.UserId, s.User.FullName, s.User.Email, s.User.Phone, s.StaffType, s.IsActive && s.User.IsActive)), query);
    }
    public async Task<StaffDto> CreateAsync(Guid ownerId, Guid depotId, CreateStaffDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        if (Encoding.UTF8.GetByteCount(dto.Password) > 72) throw new ArgumentException("Mật khẩu vượt quá 72 byte UTF-8.");
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email)) throw new DepotConflictException("Email đã được sử dụng.");
        var user = new User { FullName = dto.FullName.Trim(), Email = email, Phone = dto.Phone.Trim(),
            Role = dto.Role, PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password) };
        var staff = new DepotStaff { DepotId = depotId, User = user, StaffType = dto.Role };
        db.DepotStaffs.Add(staff);
        // Một giao dịch SaveChanges lưu đồng thời tài khoản và liên kết nhân viên với kho.
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new DepotConflictException("Email đã được sử dụng."); }
        return new(staff.Id, user.Id, user.FullName, user.Email, user.Phone, staff.StaffType, staff.IsActive);
    }
    public async Task<StaffDto> UpdateAsync(Guid ownerId, Guid depotId, Guid staffId, UpdateStaffDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        var staff = await db.DepotStaffs.Include(s => s.User).SingleOrDefaultAsync(s => s.Id == staffId && s.DepotId == depotId)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên trong kho.");
        staff.User.FullName = dto.FullName.Trim(); staff.User.Phone = dto.Phone.Trim();
        staff.User.IsActive = dto.IsActive; staff.IsActive = dto.IsActive; staff.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new(staff.Id, staff.UserId, staff.User.FullName, staff.User.Email, staff.User.Phone, staff.StaffType, staff.IsActive);
    }
}
