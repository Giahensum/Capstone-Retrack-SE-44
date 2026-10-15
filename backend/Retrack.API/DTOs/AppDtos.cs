using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs
{
    // ===== AUTH =====
    public class LoginDto
    {
        [Required, EmailAddress, MaxLength(255)]
        public string Email { get; set; } = string.Empty;
        [Required, MaxLength(128)]
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

    public class ForgotPasswordDto
    {
        [Required, EmailAddress, MaxLength(255)]
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyOtpDto
    {
        [Required, EmailAddress, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string Otp { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        [Required, EmailAddress, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string Otp { get; set; } = string.Empty;

        [Required, MinLength(6), MaxLength(72)]
        public string NewPassword { get; set; } = string.Empty;
    }

    // ===== PICKUP REQUEST =====
    public class CreatePickupRequestDto
    {
        public Guid? TargetDepotId { get; set; }

        [MaxLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Địa chỉ thu gom không được để trống.")]
        [MaxLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự.")]
        public string Address { get; set; } = string.Empty;

        [Range(-90.0, 90.0, ErrorMessage = "Vĩ độ phải nằm trong khoảng -90 đến 90.")]
        public decimal? Latitude { get; set; }

        [Range(-180.0, 180.0, ErrorMessage = "Kinh độ phải nằm trong khoảng -180 đến 180.")]
        public decimal? Longitude { get; set; }

        public DateTime? PreferredDatetime { get; set; }

        [MaxLength(1000, ErrorMessage = "URL ảnh không được vượt quá 1000 ký tự.")]
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
        public string? RequestImageUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
        public string? PaymentProofUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<PickupRequestItemDto> Items { get; set; } = new();

        // Review đã gửi (nếu có)
        public int? ReviewRating { get; set; }
        public string? ReviewComment { get; set; }
        public DateTime? ReviewCreatedAt { get; set; }
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
        [Required(ErrorMessage = "Vui lòng chọn số sao đánh giá.")]
        [Range(1, 5, ErrorMessage = "Số sao đánh giá phải từ 1 đến 5 sao.")]
        public int Rating { get; set; }

        [MaxLength(1000, ErrorMessage = "Nội dung nhận xét không được vượt quá 1000 ký tự.")]
        public string? Comment { get; set; }
    }

    // ===== SELLER PROFILE =====
    public class UpdateSellerProfileDto
    {
        [MaxLength(255, ErrorMessage = "Họ và tên không được vượt quá 255 ký tự.")]
        public string? FullName { get; set; }

        [MaxLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
        [RegularExpression(@"^(\+84|0)[0-9]{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (phải gồm 10-11 số, bắt đầu bằng 0 hoặc +84).")]
        public string? Phone { get; set; }

        [MaxLength(100, ErrorMessage = "Tên ngân hàng không được vượt quá 100 ký tự.")]
        public string? BankName { get; set; }

        [MaxLength(50, ErrorMessage = "Số tài khoản không được vượt quá 50 ký tự.")]
        public string? BankAccountNumber { get; set; }

        [MaxLength(255, ErrorMessage = "Tên chủ tài khoản không được vượt quá 255 ký tự.")]
        public string? BankAccountName { get; set; }

        [MaxLength(1000, ErrorMessage = "Đường dẫn mã QR không được vượt quá 1000 ký tự.")]
        public string? BankQrUrl { get; set; }
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

