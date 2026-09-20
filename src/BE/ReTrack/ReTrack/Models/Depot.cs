using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReTrack.Models
{
    [Table("depots")]
    public class Depot
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
        public ICollection<DepotStaff> Staffs { get; set; } = new List<DepotStaff>();
        public ICollection<PickupRequest> PickupRequests { get; set; } = new List<PickupRequest>();
        public ICollection<InventoryBatch> InventoryBatches { get; set; } = new List<InventoryBatch>();
        public ICollection<FactoryDepotPartnership> Partnerships { get; set; } = new List<FactoryDepotPartnership>();
    }
}
