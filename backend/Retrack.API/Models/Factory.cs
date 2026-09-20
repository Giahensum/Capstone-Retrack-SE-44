using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("factories")]
    public class Factory
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [Column("latitude")]
        public decimal? Latitude { get; set; }

        [Column("longitude")]
        public decimal? Longitude { get; set; }

        [Column("rating")]
        public decimal Rating { get; set; } = 0.0m;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("OwnerId")]
        public User Owner { get; set; } = null!;
        public ICollection<FactoryDemand> Demands { get; set; } = new List<FactoryDemand>();
        public ICollection<FactoryDepotPartnership> Partnerships { get; set; } = new List<FactoryDepotPartnership>();
        public ICollection<BatchQualityCheck> QualityChecks { get; set; } = new List<BatchQualityCheck>();
    }
}

