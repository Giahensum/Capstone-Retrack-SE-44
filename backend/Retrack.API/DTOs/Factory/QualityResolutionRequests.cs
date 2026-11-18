using System.ComponentModel.DataAnnotations;
namespace Retrack.API.DTOs.Factory;

public sealed class QualityResolutionRequest
{
    [Required, RegularExpression("^(RETURN|RENEGOTIATE)$")] public string Resolution { get; set; } = "";
    [Required, StringLength(2000)] public string Note { get; set; } = "";
}
public sealed class ReturnHandoverRequest
{
    [Required, StringLength(200)] public string Reference { get; set; } = "";
    [Required, StringLength(1000)] public string ProofUrl { get; set; } = "";
    [Required, StringLength(2000)] public string Note { get; set; } = "";
}
