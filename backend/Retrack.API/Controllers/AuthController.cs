using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Services;

namespace Retrack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly AppDbContext _db;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, AppDbContext db)
        {
            _authService = authService;
            _logger = logger;
            _db = db;
        }

        /// <summary>Đăng nhập bằng Email/Password</summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            if (result == null)
                return Unauthorized(ApiResponse<string>.Fail("Email hoặc mật khẩu không đúng."));

            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập thành công."));
        }

        /// <summary>Đăng ký tài khoản mới</summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(ApiResponse<string>.Fail("Email và mật khẩu không được để trống."));

            if (dto.Password.Length < 6)
                return BadRequest(ApiResponse<string>.Fail("Mật khẩu phải có ít nhất 6 ký tự."));

            var result = await _authService.RegisterAsync(dto);
            if (string.Equals(result.Role, "FACTORY", StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _db.Factories.AnyAsync(x => x.OwnerId == result.UserId);
                if (!exists)
                {
                    _db.Factories.Add(new Retrack.API.Models.Factory
                    {
                        OwnerId = result.UserId,
                        Name = result.FullName,
                        Address = "Chưa cập nhật"
                    });
                    await _db.SaveChangesAsync();
                }
            }
            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng ký thành công."));
        }

        /// <summary>
        /// Đăng nhập bằng Google — FE gửi lên idToken từ Google Sign-In SDK.
        /// BE verify token, tìm hoặc tạo user (role SELLER mặc định), trả JWT.
        /// </summary>
        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.IdToken))
                return BadRequest(ApiResponse<string>.Fail("Google token không hợp lệ."));

            var result = await _authService.GoogleLoginAsync(dto.IdToken);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập Google thành công."));
        }
    }
}

