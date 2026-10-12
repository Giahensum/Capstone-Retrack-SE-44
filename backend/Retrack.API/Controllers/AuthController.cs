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

        /// <summary>Yêu cầu gửi mã OTP đặt lại mật khẩu về email</summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(ApiResponse<string>.Fail("Vui lòng cung cấp địa chỉ email."));

            try
            {
                await _authService.ForgotPasswordAsync(dto.Email);
                return Ok(ApiResponse<string>.Ok("Mã xác nhận OTP đã được gửi đến email của bạn."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<string>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý quên mật khẩu cho {Email}", dto.Email);
                return StatusCode(500, ApiResponse<string>.Fail("Không thể gửi mã xác nhận. Vui lòng thử lại sau."));
            }
        }

        /// <summary>Xác thực mã OTP</summary>
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp))
                return BadRequest(ApiResponse<string>.Fail("Email và mã OTP không được để trống."));

            var isValid = await _authService.VerifyOtpAsync(dto.Email, dto.Otp);
            if (!isValid)
                return BadRequest(ApiResponse<string>.Fail("Mã OTP không chính xác hoặc đã hết hạn."));

            return Ok(ApiResponse<string>.Ok("Mã OTP hợp lệ."));
        }

        /// <summary>Đặt lại mật khẩu mới với mã OTP</summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập đầy đủ email, mã OTP và mật khẩu mới."));

            if (dto.NewPassword.Length < 6)
                return BadRequest(ApiResponse<string>.Fail("Mật khẩu mới phải có ít nhất 6 ký tự."));

            try
            {
                await _authService.ResetPasswordAsync(dto.Email, dto.Otp, dto.NewPassword);
                return Ok(ApiResponse<string>.Ok("Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đặt lại mật khẩu cho {Email}", dto.Email);
                return StatusCode(500, ApiResponse<string>.Fail("Có lỗi xảy ra khi đặt lại mật khẩu. Vui lòng thử lại."));
            }
        }
    }
}

