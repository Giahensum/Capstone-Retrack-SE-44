using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces
{
    public interface IPasswordResetOtpRepository
    {
        Task InvalidateActiveOtpsAsync(string email);
        Task CreateAsync(PasswordResetOtp otp);
        Task<PasswordResetOtp?> GetValidOtpAsync(string email, string otp);
        Task SaveChangesAsync();
    }
}
