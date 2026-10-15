using System.Text.Json.Serialization;

namespace Retrack.API.DTOs
{
    public class SellerProfileDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankQrUrl { get; set; }
    }

    public class SellerStatsDto
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int InProgress { get; set; }
        public int Done { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal MonthlyIncome { get; set; }
    }

    public class NearbyDepotDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public double AvgRating { get; set; }
        public int TotalDone { get; set; }
        public Dictionary<string, decimal> Prices { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DistanceKm { get; set; }
    }
}
