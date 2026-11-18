using Retrack.API.Controllers.Driver;
using Retrack.API.Controllers.Employee;
using Retrack.API.Data;
using Retrack.API.Repositories;
using Retrack.API.Services.Driver;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Tests;

/// <summary>Khởi tạo dependency mới trên đúng context do fixture sở hữu.</summary>
internal static class StaffTestServices
{
    public static EmployeeCollectionService Collection(AppDbContext db, ICloudinaryService images) =>
        new(new EmployeeCollectionRepository(db), new StaffUnitOfWork(db), images);
    public static EmployeeReportingService Reports(AppDbContext db) => new(new EmployeeReportingRepository(db));
    public static EmployeeDispatchService Dispatch(AppDbContext db) => new(new EmployeeDispatchRepository(db), new StaffUnitOfWork(db));
    public static DriverJobService Jobs(AppDbContext db) =>
        new(new DriverJobRepository(db), new DriverDeliveryRepository(db), new StaffUnitOfWork(db));
    public static DriverReportingService DriverReports(AppDbContext db, DriverJobService? jobs = null) =>
        new(new DriverReportingRepository(db, new DriverJobRepository(db)), jobs ?? Jobs(db));
    public static DriverDeliveryService Delivery(AppDbContext db, DriverJobService jobs, ICloudinaryService images) =>
        new(new DriverDeliveryRepository(db), new DriverJobRepository(db), new StaffUnitOfWork(db), jobs, images);
    public static EmployeePickupController Pickup(AppDbContext db, INotificationService? notifications = null) =>
        new(new EmployeePickupService(new EmployeePickupRepository(db), Reports(db), new StaffUnitOfWork(db), notifications ?? new NotificationService(db)));
    public static DriverNoticeController Notices(AppDbContext db, DriverJobService jobs) =>
        new(new DriverNoticeService(new DriverNoticeRepository(db), jobs));
}
