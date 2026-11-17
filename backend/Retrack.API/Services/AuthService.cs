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
        Task<bool> IsPhoneAvailableAsync(string phone);
        Task<bool> IsEmailAvailableAsync(string email);
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
            var normalizedEmail = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var user = await _userRepo.GetByEmailAsync(normalizedEmail);
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

            var fullName = dto.FullName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Họ và tên không được để trống.");
            if (fullName.Length < 2 || fullName.Length > 100)
                throw new ArgumentException("Họ và tên phải có độ dài từ 2 đến 100 ký tự.");

            var normalizedEmail = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                throw new ArgumentException("Email không được để trống.");

            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
            if (!emailRegex.IsMatch(normalizedEmail))
                throw new ArgumentException("Email không đúng định dạng.");

            var phone = dto.Phone?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException("Số điện thoại không được để trống.");

            var cleanPhone = phone.Replace(" ", "").Replace("-", "").Replace(".", "");
            if (cleanPhone.StartsWith("+84"))
                cleanPhone = "0" + cleanPhone.Substring(3);

            var phoneRegex = new System.Text.RegularExpressions.Regex(@"^0(3[2-9]|5[25689]|7[06-9]|8[1-9]|9[0-9])\d{7}$");
            if (!phoneRegex.IsMatch(cleanPhone))
                throw new ArgumentException("Số điện thoại không hợp lệ (phải là 10 chữ số thuộc các đầu số di động Việt Nam, VD: 0912345678).");

            // Kiểm tra mật khẩu chuẩn độ an toàn cao
            ValidatePassword(dto.Password);

            // Kiểm tra trùng lặp Email
            var existingEmail = await _userRepo.GetByEmailAsync(normalizedEmail);
            if (existingEmail != null)
                throw new InvalidOperationException("Email này đã được đăng ký. Vui lòng đăng nhập hoặc sử dụng email khác.");

            // Kiểm tra trùng lặp Số điện thoại
            var existingPhone = await _userRepo.GetByPhoneAsync(cleanPhone);
            if (existingPhone != null)
                throw new InvalidOperationException("Số điện thoại này đã được đăng ký trong hệ thống. Vui lòng đăng nhập hoặc sử dụng số khác.");

            var user = new User
            {
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = fullName,
                Phone = cleanPhone,
                Role = role,
                IsActive = true
            };

            await _userRepo.CreateAsync(user);
            return BuildResponse(user);
        }

        public async Task<bool> IsPhoneAvailableAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            var clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
            if (clean.StartsWith("+84")) clean = "0" + clean.Substring(3);
            var user = await _userRepo.GetByPhoneAsync(clean);
            return user == null;
        }

        public async Task<bool> IsEmailAvailableAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var clean = email.Trim().ToLowerInvariant();
            var user = await _userRepo.GetByEmailAsync(clean);
            return user == null;
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
            ValidatePassword(newPassword);

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

        private static void ValidatePassword(string? password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Mật khẩu không được để trống.");

            if (password.Length < 8)
                throw new ArgumentException("Mật khẩu phải có ít nhất 8 ký tự.");

            if (password.Length > 64)
                throw new ArgumentException("Mật khẩu không được vượt quá 64 ký tự.");

            if (Encoding.UTF8.GetByteCount(password) > 72)
                throw new ArgumentException("Mật khẩu quá dài (vượt quá 72 byte).");

            if (password.Contains(" "))
                throw new ArgumentException("Mật khẩu không được chứa khoảng trắng.");

            if (!password.Any(char.IsUpper))
                throw new ArgumentException("Mật khẩu phải chứa ít nhất 1 chữ cái in hoa (A-Z).");

            if (!password.Any(char.IsLower))
                throw new ArgumentException("Mật khẩu phải chứa ít nhất 1 chữ cái thường (a-z).");

            if (!password.Any(char.IsDigit))
                throw new ArgumentException("Mật khẩu phải chứa ít nhất 1 chữ số (0-9).");

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                throw new ArgumentException("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (@, #, $, %, !, &, *, ...).");
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

