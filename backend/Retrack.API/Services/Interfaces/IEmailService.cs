namespace Retrack.API.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendPasswordResetOtpAsync(string toEmail, string otpCode, string? recipientName = null);
    }
}
