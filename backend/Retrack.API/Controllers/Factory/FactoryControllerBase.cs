using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Retrack.API.Controllers.Factory;

[ApiController]
public abstract class FactoryControllerBase : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token không có user id hợp lệ.");
        }
    }
}
