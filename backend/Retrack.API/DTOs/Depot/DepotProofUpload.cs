using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Depot;

public sealed class DepotProofUpload
{
    [Required]
    public IFormFile File { get; set; } = null!;
}
