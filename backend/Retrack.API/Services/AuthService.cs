using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.IdentityModel.Tokens;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories;

namespace Retrack.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> GoogleLoginAsync(string googleIdToken);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IConfiguration _config;

        public AuthService(IUserRepository userRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _config = config;
        }

        // ── Email / Password Login ──────────────────────────────────────
        public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
        {
            var user = await _userRepo.GetByEmailAsync(dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return null;

            if (!user.IsActive)
                throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

            return BuildResponse(user);
        }

        // ── Register ────────────────────────────────────────────────────
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            var existing = await _userRepo.GetByEmailAsync(dto.Email);
            if (existing != null)
                throw new InvalidOperationException("Email đã được sử dụng.");

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Phone = dto.Phone,
                Role = dto.Role.ToUpper(),
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

