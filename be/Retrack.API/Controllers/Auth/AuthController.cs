using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Retrack.API.Data;
using Retrack.API.DTOs.Auth;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using FactoryEntity = Retrack.API.Models.Factory;

namespace Retrack.API.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    private readonly PasswordHasher<User> _hasher = new();

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role == UserRole.ADMIN) return BadRequest(new { success = false, message = "Không thể tự đăng ký tài khoản quản trị." });
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email.ToLower() == email, ct)) return Conflict(new { success = false, message = "Email đã được sử dụng." });
        var user = new User { Id = Guid.NewGuid(), Email = email, FullName = request.FullName.Trim(), Phone = request.Phone?.Trim(), Role = request.Role, IsActive = true };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        if (request.Role == UserRole.FACTORY) db.Factories.Add(new FactoryEntity { Id = Guid.NewGuid(), UserId = user.Id, CompanyName = request.FullName.Trim() });
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, new { success = true, data = new { user.Id, user.Email, user.FullName, role = user.Role.ToString() } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == email, ct);
        if (user is null || !user.IsActive || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { success = false, message = "Email hoặc mật khẩu không đúng." });
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return Ok(new { success = true, data = new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresAt = token.ValidTo, user = new { user.Id, user.Email, user.FullName, role = user.Role.ToString() } } });
    }
}
