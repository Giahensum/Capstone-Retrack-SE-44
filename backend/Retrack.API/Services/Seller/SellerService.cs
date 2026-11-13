using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Seller
{
    public class SellerService : ISellerService
    {
        private readonly ISellerRepository _sellerRepo;
        private readonly IDepotUnitOfWork _work;

        public SellerService(ISellerRepository sellerRepo, IDepotUnitOfWork work)
        {
            _sellerRepo = sellerRepo;
            _work = work;
        }

        public async Task<SellerProfileDto?> GetProfileAsync(Guid sellerId)
        {
            var user = await _sellerRepo.GetSellerProfileAsync(sellerId);
            if (user == null) return null;

            return new SellerProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                BankName = user.BankName,
                BankAccountNumber = user.BankAccountNumber,
                BankAccountName = user.BankAccountName,
                BankQrUrl = user.BankQrUrl
            };
        }

        public async Task<bool> UpdateProfileAsync(Guid sellerId, UpdateSellerProfileDto dto)
        {
            var user = await _sellerRepo.GetSellerProfileAsync(sellerId);
            if (user == null) return false;

            if (dto.FullName != null)
            {
                if (string.IsNullOrWhiteSpace(dto.FullName))
                    throw new ArgumentException("Họ và tên không được để trống.");
                if (dto.FullName.Length > 255)
                    throw new ArgumentException("Họ và tên không được vượt quá 255 ký tự.");
                user.FullName = dto.FullName.Trim();
            }

            if (dto.Phone != null)
            {
                var cleanPhone = dto.Phone.Trim();
                if (!string.IsNullOrEmpty(cleanPhone))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(cleanPhone, @"^(\+84|0)[0-9]{9,10}$"))
                        throw new ArgumentException("Số điện thoại không hợp lệ (phải gồm 10-11 số, bắt đầu bằng 0 hoặc +84).");
                    user.Phone = cleanPhone;
                }
                else
                {
                    user.Phone = string.Empty;
                }
            }

            // Bank information (can be null if cleared)
            if (dto.BankName != null)
            {
                if (dto.BankName.Length > 100)
                    throw new ArgumentException("Tên ngân hàng không được vượt quá 100 ký tự.");
                user.BankName = string.IsNullOrWhiteSpace(dto.BankName) ? null : dto.BankName.Trim();
            }

            if (dto.BankAccountNumber != null)
            {
                var cleanAcc = dto.BankAccountNumber.Trim();
                if (!string.IsNullOrEmpty(cleanAcc))
                {
                    if (cleanAcc.Length > 50)
                        throw new ArgumentException("Số tài khoản không được vượt quá 50 ký tự.");
                    if (!System.Text.RegularExpressions.Regex.IsMatch(cleanAcc, @"^[a-zA-Z0-9]+$"))
                        throw new ArgumentException("Số tài khoản chỉ được chứa chữ cái và số.");
                    user.BankAccountNumber = cleanAcc;
                }
                else
                {
                    user.BankAccountNumber = null;
                }
            }

            if (dto.BankAccountName != null)
            {
                if (dto.BankAccountName.Length > 255)
                    throw new ArgumentException("Tên chủ tài khoản không được vượt quá 255 ký tự.");
                user.BankAccountName = string.IsNullOrWhiteSpace(dto.BankAccountName) ? null : dto.BankAccountName.Trim().ToUpperInvariant();
            }

            if (dto.BankQrUrl != null)
            {
                var cleanUrl = dto.BankQrUrl.Trim();
                if (!string.IsNullOrEmpty(cleanUrl))
                {
                    if (cleanUrl.Length > 1000)
                        throw new ArgumentException("Đường dẫn mã QR không được vượt quá 1000 ký tự.");
                    if (!Uri.TryCreate(cleanUrl, UriKind.Absolute, out var uriResult) ||
                        (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                        throw new ArgumentException("Đường dẫn mã QR phải là URL hợp lệ (http hoặc https).");
                    user.BankQrUrl = cleanUrl;
                }
                else
                {
                    user.BankQrUrl = null;
                }
            }

            await _sellerRepo.UpdateSellerProfileAsync(user);
            return true;
        }

        public async Task<(bool Success, string Message, int StatusCode)> CancelRequestAsync(Guid requestId, Guid sellerId)
        {
            await using var tx = await _work.BeginAsync();
            var req = await _sellerRepo.LockPickupAsync(requestId);
            if (req == null)
                return (false, "Không tìm thấy.", 404);

            if (req.SellerId != sellerId)
                return (false, "Không có quyền thực hiện.", 403);

            if (req.Status != "PENDING")
                return (false, "Chỉ hủy được đơn ở trạng thái chờ xử lý.", 400);

            req.Status = "CANCELLED";
            _sellerRepo.AddNotice(Notification.ForPickup(sellerId, req.Id, "Đã hủy đơn thu gom", "Đơn thu gom của bạn đã được hủy."));
            await _sellerRepo.UpdatePickupRequestAsync(req);
            await tx.CommitAsync();
            return (true, "Đã hủy đơn thành công.", 200);
        }

        public async Task<(bool Success, string Message, int StatusCode)> RejectWeighAsync(Guid requestId, Guid sellerId)
        {
            await using var tx = await _work.BeginAsync();
            var req = await _sellerRepo.LockPickupAsync(requestId);
            if (req == null)
                return (false, "Không tìm thấy.", 404);

            if (req.SellerId != sellerId)
                return (false, "Không có quyền thực hiện.", 403);

            if (req.Status != "WEIGHED")
                return (false, "Đơn chưa ở trạng thái chờ xác nhận.", 400);

            req.Status = "SCHEDULED"; // Trả về cho nhân viên cân lại
            _sellerRepo.AddNotice(Notification.ForPickup(sellerId, req.Id, "Đã yêu cầu cân lại", "Bạn đã từ chối kết quả cân. Đơn đã được trả về cho nhân viên xử lý lại."));
            if (req.AcceptedCollectorId.HasValue)
                _sellerRepo.AddNotice(Notification.ForPickup(req.AcceptedCollectorId.Value, req.Id, "Người bán yêu cầu cân lại", "Người bán đã từ chối kết quả cân. Hãy mở lại đơn và kiểm tra kết quả với người bán."));
            await _sellerRepo.UpdatePickupRequestAsync(req);
            await tx.CommitAsync();
            return (true, "Đã từ chối. Nhân viên sẽ cân lại.", 200);
        }

        public async Task<List<NearbyDepotDto>> GetNearbyDepotsAsync(decimal? lat, decimal? lng)
        {
            if (lat.HasValue != lng.HasValue)
                throw new ArgumentException("Cần cung cấp đồng thời cả vĩ độ (lat) và kinh độ (lng).");

            if (lat.HasValue && (lat.Value < -90 || lat.Value > 90))
                throw new ArgumentException("Vĩ độ (lat) phải nằm trong khoảng từ -90 đến 90.");

            if (lng.HasValue && (lng.Value < -180 || lng.Value > 180))
                throw new ArgumentException("Kinh độ (lng) phải nằm trong khoảng từ -180 đến 180.");

            return await _sellerRepo.GetNearbyDepotsAsync(lat, lng);
        }

        public async Task<(bool Success, string Message, int StatusCode)> ReviewDepotAsync(Guid requestId, Guid sellerId, CreateReviewDto dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
                return (false, "Đánh giá phải từ 1 đến 5 sao.", 400);

            if (dto.Comment != null && dto.Comment.Length > 1000)
                return (false, "Nội dung nhận xét không được vượt quá 1000 ký tự.", 400);

            var req = await _sellerRepo.GetPickupRequestByIdAsync(requestId);
            if (req == null)
                return (false, "Không tìm thấy.", 404);

            if (req.SellerId != sellerId)
                return (false, "Không có quyền thực hiện.", 403);

            if (req.Status != "DONE")
                return (false, "Đơn chưa hoàn thành.", 400);

            if (req.TargetDepotId == null)
                return (false, "Đơn không có kho vựa.", 400);

            var existing = await _sellerRepo.HasSellerReviewedRequestAsync(requestId);
            if (existing)
                return (false, "Đã đánh giá rồi.", 400);

            var review = new SellerDepotReview
            {
                PickupRequestId = requestId,
                DepotId = req.TargetDepotId.Value,
                Rating = dto.Rating,
                Comment = dto.Comment?.Trim()
            };

            await _sellerRepo.AddReviewAsync(review);
            return (true, "Đánh giá thành công.", 200);
        }

        public async Task<SellerStatsDto> GetStatsAsync(Guid sellerId)
        {
            return await _sellerRepo.GetSellerStatsAsync(sellerId);
        }
    }
}
