using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories
{
    public class PasswordResetOtpRepository : IPasswordResetOtpRepository
    {
        private readonly AppDbContext _db;

        public PasswordResetOtpRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task InvalidateActiveOtpsAsync(string email)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var activeOtps = await _db.PasswordResetOtps
                .Where(x => x.Email.ToLower() == normalizedEmail && !x.IsUsed && x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var item in activeOtps)
            {
                item.IsUsed = true;
            }
            if (activeOtps.Count > 0)
            {
                await _db.SaveChangesAsync();
            }
        }

        public async Task CreateAsync(PasswordResetOtp otp)
        {
            _db.PasswordResetOtps.Add(otp);
            await _db.SaveChangesAsync();
        }

        public async Task<PasswordResetOtp?> GetValidOtpAsync(string email, string otp)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var cleanOtp = otp.Trim();

            return await _db.PasswordResetOtps
                .Where(x => x.Email.ToLower() == normalizedEmail && x.OtpCode == cleanOtp && !x.IsUsed && x.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
