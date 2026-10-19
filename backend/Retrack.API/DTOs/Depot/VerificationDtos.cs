namespace Retrack.API.DTOs.Depot;

public sealed class CreateDepotDto
{
    public Guid OperationId { get; set; }
    public UpdateDepotProfileDto Profile { get; set; } = new();
}
public record VerificationFileDto(Guid Id, string Name, string ContentType, int Size);
public record VerificationSubmissionDto(Guid Id, Guid DepotId, string DepotName, string Status, string Note,
    DateTime SubmittedAt, DateTime? ReviewedAt, string? ReviewReason, List<VerificationFileDto> Files, string DepotAddress, string? TaxCode);
public record VerificationReviewDto(bool Approve, string Reason);
