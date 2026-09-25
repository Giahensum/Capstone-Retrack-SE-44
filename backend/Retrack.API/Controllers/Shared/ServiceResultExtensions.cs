using Microsoft.AspNetCore.Mvc;
using Retrack.API.Services.Shared;

namespace Retrack.API.Controllers.Shared;

public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, ServiceResult result,
        Func<Guid, string>? createdLocation = null)
        => MapResult(controller, result, null, createdLocation);

    public static IActionResult ToActionResult<T>(this ControllerBase controller, ServiceResult<T> result,
        Func<Guid, string>? createdLocation = null) where T : class
        => MapResult(controller, result, result.Data, createdLocation);

    private static IActionResult MapResult(ControllerBase controller, ServiceResult result, object? data,
        Func<Guid, string>? createdLocation)
    {
        var body = new Dictionary<string, object?> { ["success"] = result.Outcome == ServiceOutcome.Success };
        if (result.Message is not null) body["message"] = result.Message;
        if (data is not null) body["data"] = data;

        return result.Outcome switch
        {
            ServiceOutcome.Success when createdLocation is not null && result.ResourceId.HasValue
                => controller.Created(createdLocation(result.ResourceId.Value), body),
            ServiceOutcome.Success => controller.Ok(body),
            ServiceOutcome.Invalid => controller.BadRequest(body),
            ServiceOutcome.NotFound => controller.NotFound(body),
            ServiceOutcome.Conflict => controller.Conflict(body),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }
}
