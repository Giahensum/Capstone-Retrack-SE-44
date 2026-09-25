namespace Retrack.API.DTOs.Factory;

public sealed record ProfileOwnerResponse
{
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
