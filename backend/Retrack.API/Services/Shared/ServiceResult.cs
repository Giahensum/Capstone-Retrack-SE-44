namespace Retrack.API.Services.Shared;

public enum ServiceOutcome
{
    Success,
    Invalid,
    NotFound,
    Conflict
}

/// <summary>A business outcome without a response payload.</summary>
public record ServiceResult(ServiceOutcome Outcome, string? Message = null, Guid? ResourceId = null)
{
    public static ServiceResult Success(string? message = null) => new(ServiceOutcome.Success, message);
    public static ServiceResult Invalid(string message) => new(ServiceOutcome.Invalid, message);
    public static ServiceResult NotFound(string message) => new(ServiceOutcome.NotFound, message);
    public static ServiceResult Conflict(string message) => new(ServiceOutcome.Conflict, message);
}

/// <summary>A typed business outcome; HTTP mapping stays in the controller layer.</summary>
public sealed record ServiceResult<T>(ServiceOutcome Outcome, T? Data = null, string? Message = null,
    Guid? ResourceId = null) : ServiceResult(Outcome, Message, ResourceId) where T : class
{
    public static ServiceResult<T> Success(T data, string? message = null, Guid? resourceId = null)
        => new(ServiceOutcome.Success, data, message, resourceId);

    public new static ServiceResult<T> Invalid(string message) => new(ServiceOutcome.Invalid, Message: message);
    public new static ServiceResult<T> NotFound(string message) => new(ServiceOutcome.NotFound, Message: message);
    public new static ServiceResult<T> Conflict(string message) => new(ServiceOutcome.Conflict, Message: message);
}
