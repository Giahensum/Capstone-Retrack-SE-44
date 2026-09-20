using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("email")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [Column("role")]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty; // SELLER, DEPOT_OWNER, DEPOT_EMPLOYEE, DRIVER, FACTORY, ADMIN

        [Required]
        [Column("full_name")]
        [MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Column("phone")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Depot> OwnedDepots { get; set; } = new List<Depot>();
        public ICollection<Factory> OwnedFactories { get; set; } = new List<Factory>();
        public ICollection<DepotStaff> DepotStaffs { get; set; } = new List<DepotStaff>();
        public ICollection<PickupRequest> PickupRequests { get; set; } = new List<PickupRequest>();
        public ICollection<TransportJob> TransportJobs { get; set; } = new List<TransportJob>();
    }
}

