using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Shared
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendPasswordResetOtpAsync(string toEmail, string otpCode, string? recipientName = null)
        {
            var host = _config["Smtp:Host"];
            var portStr = _config["Smtp:Port"];
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var fromEmail = _config["Smtp:FromEmail"] ?? "noreply@retrack.vn";
            var fromName = _config["Smtp:FromName"] ?? "ReTrack Platform";
            var port = int.TryParse(portStr, out var p) ? p : 587;

            var greeting = string.IsNullOrWhiteSpace(recipientName) ? "Xin chào quý khách," : $"Xin chào {recipientName},";
            var subject = $"[ReTrack] Mã xác nhận đặt lại mật khẩu của bạn: {otpCode}";

            var bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Mã OTP ReTrack</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f4f6f8; margin: 0; padding: 20px; }}
        .container {{ max-width: 540px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.08); }}
        .header {{ background: linear-gradient(135deg, #166534, #15803d); padding: 32px 24px; text-align: center; color: #ffffff; }}
        .header h1 {{ margin: 0; font-size: 26px; font-weight: 800; letter-spacing: 1px; }}
        .content {{ padding: 32px 28px; color: #1e293b; line-height: 1.6; }}
        .otp-box {{ background: #f0fdf4; border: 2px dashed #22c55e; border-radius: 12px; padding: 20px; text-align: center; margin: 24px 0; }}
        .otp-code {{ font-size: 36px; font-weight: 800; color: #15803d; letter-spacing: 8px; margin: 0; font-family: 'Courier New', monospace; }}
        .notice {{ font-size: 13px; color: #64748b; margin-top: 8px; }}
        .warning {{ background: #fffbeb; border-left: 4px solid #f59e0b; padding: 12px 16px; border-radius: 4px; font-size: 13px; color: #92400e; margin: 20px 0; }}
        .footer {{ background: #f8fafc; padding: 20px 24px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>♻️ RETRACK</h1>
            <p style=""margin: 6px 0 0; opacity: 0.9; font-size: 14px;"">Hệ thống Quản lý Tái chế &amp; Thu gom Phế liệu</p>
        </div>
        <div class=""content"">
            <p style=""font-size: 16px; font-weight: 600;"">{greeting}</p>
            <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản liên kết với địa chỉ email <strong>{toEmail}</strong>.</p>
            <p>Vui lòng sử dụng mã xác thực (OTP) dưới đây để hoàn tất việc đổi mật khẩu mới:</p>
            
            <div class=""otp-box"">
                <div class=""otp-code"">{otpCode}</div>
                <div class=""notice"">Mã có hiệu lực trong vòng <strong>10 phút</strong></div>
            </div>

            <div class=""warning"">
                🔒 <strong>Lưu ý bảo mật:</strong> Tuyệt đối không cung cấp mã OTP này cho bất kỳ ai, kể cả nhân viên hỗ trợ ReTrack. Nếu bạn không gửi yêu cầu này, vui lòng bỏ qua email và bảo vệ tài khoản của bạn.
            </div>

            <p style=""margin-top: 24px; font-size: 14px; color: #475569;"">Trân trọng,<br><strong>Đội ngũ ReTrack</strong></p>
        </div>
        <div class=""footer"">
            <p>© {DateTime.UtcNow.Year} ReTrack Platform. Mọi quyền được bảo lưu.</p>
            <p>Email này được gửi tự động, vui lòng không phản hồi trực tiếp.</p>
        </div>
    </div>
</body>
</html>";

            // Luôn log OTP ra console để tiện test
            _logger.LogInformation("=================================================");
            _logger.LogInformation("[RETRACK OTP EMAIL] To: {Email} | OTP Code: {Otp}", toEmail, otpCode);
            _logger.LogInformation("=================================================");

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogInformation("SMTP chưa cấu hình đầy đủ. OTP đã được in ra console để test.");
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(recipientName ?? toEmail, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = bodyHtml };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();

                // Kết nối với STARTTLS (port 587) - tương thích Gmail App Password
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("✅ Gửi email OTP thành công tới {Email} qua {Host}:{Port}", toEmail, host, port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Gửi email OTP thất bại tới {Email}. OTP đã được in ở console.", toEmail);
            }
        }
    }
}
