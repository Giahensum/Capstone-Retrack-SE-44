namespace Retrack.API.Services.Interfaces;

public interface IAuthService
{
    Task<object> RegisterAsync(object request);
    Task<object> LoginAsync(string email, string password);
    Task<object> GoogleLoginAsync(string googleToken);
}
