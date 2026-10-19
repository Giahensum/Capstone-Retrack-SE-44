using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> GoogleLoginAsync(string googleIdToken);
        Task ForgotPasswordAsync(string email);
        Task<bool> VerifyOtpAsync(string email, string otp);
        Task ResetPasswordAsync(string email, string otp, string newPassword);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IConfiguration _config;
        private readonly IEmailService? _emailService;
        private readonly Repositories.Interfaces.IPasswordResetOtpRepository? _otpRepo;

        public AuthService(IUserRepository userRepo, IConfiguration config)
            : this(userRepo, config, (Repositories.Interfaces.IPasswordResetOtpRepository)null!, null!)
        {
        }

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public AuthService(
            IUserRepository userRepo,
            IConfiguration config,
            Repositories.Interfaces.IPasswordResetOtpRepository otpRepo,
            IEmailService emailService)
        {
            _userRepo = userRepo;
            _config = config;
            _otpRepo = otpRepo;
            _emailService = emailService;
        }

        // ── Email / Password Login ──────────────────────────────────────
        public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
        {
            var user = await _userRepo.GetByEmailAsync(dto.Email.Trim());
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return null;

            if (!user.IsActive)
                throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

            return BuildResponse(user);
        }

        // ── Register ────────────────────────────────────────────────────
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            // Chủ kho tạo tài khoản nhân viên; đăng ký công khai không được cấp quyền Admin/nhân viên.
            var role = dto.Role?.Trim().ToUpperInvariant();
            if (role is not ("SELLER" or "DEPOT_OWNER" or "FACTORY"))
                throw new ArgumentException("Vai trò không được phép tự đăng ký.");
            if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Length > 255 ||
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email) ||
                dto.Password.Length < 6 || Encoding.UTF8.GetByteCount(dto.Password) > 72)
                throw new ArgumentException("Tên, email hoặc mật khẩu không hợp lệ (mật khẩu ít nhất 6 ký tự, tối đa 72 byte).");
            var existing = await _userRepo.GetByEmailAsync(dto.Email);
            if (existing != null)
                throw new InvalidOperationException("Email đã được sử dụng.");

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Phone = dto.Phone,
                Role = role,
                IsActive = true
            };

            await _userRepo.CreateAsync(user);
            return BuildResponse(user);
        }

        // ── Google OAuth Login ──────────────────────────────────────────
        public async Task<AuthResponseDto> GoogleLoginAsync(string googleIdToken)
        {
            var clientId = _config["Google:ClientId"]
                ?? throw new InvalidOperationException("Google ClientId chưa được cấu hình.");

            // Verify token với Google
            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(googleIdToken, settings);
            }
            catch
            {
                throw new UnauthorizedAccessException("Google token không hợp lệ.");
            }

            // Tìm hoặc tạo user
            var user = await _userRepo.GetByEmailAsync(payload.Email);
            if (user == null)
            {
                // Tự động tạo tài khoản SELLER khi đăng nhập Google lần đầu
                user = new User
                {
                    Email = payload.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    FullName = payload.Name ?? payload.Email,
                    Phone = "",
                    Role = "SELLER",
                    IsActive = true
                };
                await _userRepo.CreateAsync(user);
            }

            if (!user.IsActive)
                throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

            return BuildResponse(user);
        }

        // ── Forgot Password & OTP ───────────────────────────────────────
        public async Task ForgotPasswordAsync(string email)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await _userRepo.GetByEmailAsync(normalizedEmail);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài khoản với email này.");
            }

            if (!user.IsActive)
                throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

            if (_otpRepo != null)
            {
                await _otpRepo.InvalidateActiveOtpsAsync(normalizedEmail);
            }

            // Tạo mã OTP ngẫu nhiên 6 chữ số
            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var otpEntity = new PasswordResetOtp
            {
                Email = normalizedEmail,
                OtpCode = otpCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            if (_otpRepo != null)
            {
                await _otpRepo.CreateAsync(otpEntity);
            }

            // Gửi email OTP
            if (_emailService != null)
            {
                await _emailService.SendPasswordResetOtpAsync(user.Email, otpCode, user.FullName);
            }
        }

        public async Task<bool> VerifyOtpAsync(string email, string otp)
        {
            if (_otpRepo == null) return false;
            var validOtp = await _otpRepo.GetValidOtpAsync(email, otp);
            return validOtp != null;
        }

        public async Task ResetPasswordAsync(string email, string otp, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6 || Encoding.UTF8.GetByteCount(newPassword) > 72)
                throw new ArgumentException("Mật khẩu mới phải có ít nhất 6 ký tự và tối đa 72 byte.");

            if (_otpRepo == null)
                throw new InvalidOperationException("OTP repository is not configured.");

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var validOtp = await _otpRepo.GetValidOtpAsync(normalizedEmail, otp);

            if (validOtp == null)
                throw new ArgumentException("Mã OTP không hợp lệ hoặc đã hết hạn.");

            var user = await _userRepo.GetByEmailAsync(normalizedEmail);
            if (user == null)
                throw new KeyNotFoundException("Không tìm thấy tài khoản người dùng.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user);

            // Đánh dấu mã OTP đã được dùng
            validOtp.IsUsed = true;
            await _otpRepo.SaveChangesAsync();
        }

        // ── Helpers ─────────────────────────────────────────────────────
        private AuthResponseDto BuildResponse(User user) => new()
        {
            Token = GenerateJwt(user),
            Role = user.Role,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email
        };

        private string GenerateJwt(User user)
        {
            var jwtKey = _config["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key chưa được cấu hình.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(ClaimTypes.Name, user.FullName)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

