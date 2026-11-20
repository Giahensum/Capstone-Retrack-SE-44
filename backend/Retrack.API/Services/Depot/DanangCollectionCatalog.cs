using System.Security.Cryptography;
using System.Text;

namespace Retrack.API.Services.Depot;

public sealed record CollectionRegion(string Code, string Name, double Latitude, double Longitude, double RadiusMeters);

// Danh mục tham chiếu dùng chung, độc lập với seed và việc tạo kho.
// Tâm dùng tổ chức ca, không phải ranh giới hành chính hoặc điểm đích dẫn đường.
public static class DanangCollectionCatalog
{
    public static IReadOnlyList<CollectionRegion> Regions { get; } = Array.AsReadOnly(new CollectionRegion[]
    {
        new("DN-HAN", "Trần Phú – Chợ Hàn", 16.06836, 108.22443, 2500),
        new("DN-CON", "Hùng Vương – Chợ Cồn", 16.06817, 108.21453, 2500),
        new("DN-CHAM", "Bình Hiên – Bảo tàng Chăm", 16.06020, 108.22317, 2000),
        new("DN-HAIPHONG", "Hải Phòng – Bệnh viện Đà Nẵng", 16.0721056, 108.215694, 2000),
        new("DN-THACGIAN", "Thạc Gián – Ga Đà Nẵng", 16.071000, 108.209333, 2500),
        new("DN-THACHTHANG", "Thạch Thang – Trung tâm hành chính", 16.0770864, 108.2231701, 2500),
        new("DN-HOAKHANH", "Hòa Khánh – Nguyễn Lương Bằng", 16.0746638, 108.1506880, 3000),
        new("DN-HOAMINH", "Hòa Minh – Bến xe trung tâm", 16.05499, 108.17338, 3000),
        new("DN-NAMO", "Nam Ô", 16.115196, 108.130723, 3500),
        new("DN-MANTHAI", "Mân Thái", 16.0890212, 108.2495942, 2500),
        new("DN-MYKHE", "Mỹ An – Mỹ Khê", 16.0506, 108.2489, 2500),
        new("DN-HOACUONG", "Hòa Cường – Xô Viết Nghệ Tĩnh", 16.0321559, 108.2213119, 3000),
        new("DN-HOAQUY", "Hòa Quý – Ngũ Hành Sơn", 15.9874125, 108.2562344, 3500),
        new("DN-TUYLOAN", "Túy Loan – Hòa Phong", 15.9903511, 108.1409952, 5000),
        new("DN-ANSON", "An Sơn – Hòa Ninh", 16.0259108, 108.0322546, 5000),
    });

    public static CollectionRegion Find(string code) => Regions.SingleOrDefault(r => r.Code == code)
        ?? throw new ArgumentException("Vùng có sẵn không hợp lệ. Hãy tải lại danh mục.");

    // Cùng kho/vùng có ID ổn định để retry không tạo trùng; kho khác có ID khác.
    public static Guid AreaId(Guid depot, string code) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"retrack-collection-region-v1:{depot:D}:{code}"))[..16]);
}
