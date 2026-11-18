using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Depot;

public sealed class SettlementDecisionDto
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    public bool Accept { get; set; }
    [Range(0, double.MaxValue)] public decimal Amount { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
}
public record BatchWorkflowDto(int Version, int Revision, DateTime? ProposedAt, DateTime? ApprovedAt, string? DecisionReason);
public record BatchWorkflowEventDto(Guid Id, string Action, string FromStatus, string ToStatus, int Revision, string? Note, DateTime CreatedAt,
    decimal? PricePerKg = null, decimal? NetAmount = null);
