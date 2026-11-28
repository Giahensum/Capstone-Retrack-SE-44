using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;

namespace Retrack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>Đăng nhập bằng Email/Password</summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);
                if (result == null)
                    return Unauthorized(ApiResponse<string>.Fail("Email hoặc mật khẩu không đúng."));

                return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<string>.Fail(ex.Message));
            }
        }

        /// <summary>Đăng ký tài khoản mới</summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(ApiResponse<string>.Fail("Email và mật khẩu không được để trống."));

            try
            {
                var result = await _authService.RegisterAsync(dto);
                return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng ký thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
        }

        /// <summary>Kiểm tra số điện thoại đã tồn tại chưa</summary>
        [HttpGet("check-phone")]
        public async Task<IActionResult> CheckPhone([FromQuery] string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return BadRequest(ApiResponse<object>.Fail("Vui lòng cung cấp số điện thoại."));

            var clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
            if (clean.StartsWith("+84")) clean = "0" + clean.Substring(3);

            var phoneRegex = new System.Text.RegularExpressions.Regex(@"^0(3[2-9]|5[25689]|7[06-9]|8[1-9]|9[0-9])\d{7}$");
            if (!phoneRegex.IsMatch(clean))
                return BadRequest(ApiResponse<object>.Fail("Số điện thoại không đúng định dạng di động Việt Nam."));

            var available = await _authService.IsPhoneAvailableAsync(clean);
            if (!available)
                return Ok(ApiResponse<object>.Ok(new { available = false, message = "Số điện thoại này đã được đăng ký trong hệ thống." }));

            return Ok(ApiResponse<object>.Ok(new { available = true, message = "Số điện thoại có thể sử dụng." }));
        }

        /// <summary>Kiểm tra email đã tồn tại chưa</summary>
        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(ApiResponse<object>.Fail("Vui lòng cung cấp địa chỉ email."));

            var clean = email.Trim().ToLowerInvariant();
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
            if (!emailRegex.IsMatch(clean))
                return BadRequest(ApiResponse<object>.Fail("Email không đúng định dạng."));

            var available = await _authService.IsEmailAvailableAsync(clean);
            if (!available)
                return Ok(ApiResponse<object>.Ok(new { available = false, message = "Email này đã được sử dụng." }));

            return Ok(ApiResponse<object>.Ok(new { available = true, message = "Email có thể sử dụng." }));
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

            try
            {
                var result = await _authService.GoogleLoginAsync(dto.IdToken);
                return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập Google thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đăng nhập Google");
                return BadRequest(ApiResponse<string>.Fail("Đăng nhập Google không thành công. Token không hợp lệ."));
            }
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

