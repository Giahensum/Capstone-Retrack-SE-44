using System.ComponentModel.DataAnnotations;
namespace Retrack.API.DTOs.Depot;

public sealed class BatchReturnReceiptDto
{
    public bool Confirmed { get; set; }
    [Range(0.001, 1000000000)] public decimal ReceivedKg { get; set; }
    [Required, StringLength(200)] public string Reference { get; set; } = "";
    [Required, StringLength(2000)] public string Note { get; set; } = "";
}
public record BatchReturnDto(DateTime? HandedOverAt, string? HandoverProofUrl, string? HandoverReference,
    string? HandoverNote, DateTime? ReceivedAt, string? ReceiptReference, string? ReceiptNote);
