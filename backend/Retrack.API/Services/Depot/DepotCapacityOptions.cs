using System.ComponentModel.DataAnnotations;
namespace Retrack.API.Services.Depot;

// Hạn mức sản phẩm; có thể cấu hình bằng DepotCapacity__... trong môi trường triển khai.
public sealed class DepotCapacityOptions
{
    [Range(1, 1000)] public int MaxDepotsPerOwner { get; set; } = 5;
    [Range(1, 10000)] public int MaxActiveStaffPerDepot { get; set; } = 10;
    public void RequireStaffSlot(int used)
    {
        if (used >= MaxActiveStaffPerDepot)
            throw new DepotConflictException($"Kho đã đủ {MaxActiveStaffPerDepot} nhân sự đang hoạt động (thu gom và tài xế). Hãy vô hiệu hóa nhân sự không còn làm việc trước khi thêm hoặc kích hoạt lại.");
    }
}
