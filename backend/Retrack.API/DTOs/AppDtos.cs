namespace Retrack.API.DTOs
{
    // ===== AUTH =====
    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // SELLER, DEPOT_OWNER, DRIVER, etc.
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class GoogleLoginDto
    {
        public string IdToken { get; set; } = string.Empty;
    }

    // ===== PICKUP REQUEST =====
    public class CreatePickupRequestDto
    {
        public Guid? TargetDepotId { get; set; }
        public string? Description { get; set; }
        public string Address { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public DateTime? PreferredDatetime { get; set; }
        public string? RequestImageUrl { get; set; }
    }

    public class PickupRequestDto
    {
        public Guid Id { get; set; }
        public Guid SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public Guid? TargetDepotId { get; set; }
        public string? DepotName { get; set; }
        public string? Description { get; set; }
        public string Address { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public DateTime? PreferredDatetime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PickupRequestItemDto> Items { get; set; } = new();
    }

    public class PickupRequestItemDto
    {
        public string MaterialType { get; set; } = string.Empty;
        public decimal WeightKg { get; set; }
        public decimal PricePerKg { get; set; }
        public decimal SubTotal { get; set; }
    }

    public class WeighItemDto
    {
        public string MaterialType { get; set; } = string.Empty;
        public decimal WeightKg { get; set; }
        public decimal PricePerKg { get; set; }
    }

    // ===== INVENTORY BATCH =====
    public class CreateInventoryBatchDto
    {
        public Guid? TargetFactoryId { get; set; }
        public string MaterialType { get; set; } = string.Empty;
        public decimal DeclaredWeightKg { get; set; }
        public string? Description { get; set; }
    }

    public class InventoryBatchDto
    {
        public Guid Id { get; set; }
        public Guid DepotId { get; set; }
        public string DepotName { get; set; } = string.Empty;
        public Guid? TargetFactoryId { get; set; }
        public string? FactoryName { get; set; }
        public string MaterialType { get; set; } = string.Empty;
        public decimal DeclaredWeightKg { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // ===== FACTORY DEMAND =====
    public class CreateFactoryDemandDto
    {
        public string MaterialType { get; set; } = string.Empty;
        public decimal RequiredWeightKg { get; set; }
        public decimal? MinPricePerKg { get; set; }
        public decimal? MaxPricePerKg { get; set; }
        public DateTime Deadline { get; set; }
    }

    // ===== QC =====
    public class CreateQualityCheckDto
    {
        public decimal ActualWeightKg { get; set; }
        public string Grade { get; set; } = string.Empty;
        public decimal AgreedPricePerKg { get; set; }
        public bool IsAccepted { get; set; }
    }

    // ===== REVIEW =====
    public class CreateReviewDto
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

    // ===== GENERIC RESPONSE =====
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Success")
            => new() { Success = true, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message)
            => new() { Success = false, Message = message };
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}

