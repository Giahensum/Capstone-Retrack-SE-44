using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Driver;
using Retrack.API.Services.Employee;

namespace Retrack.API.Services.Staff;

public static class StaffWorkflowRegistration
{
    /// <summary>Đăng ký chung cho host thật và kiểm thử DI/HTTP; giữ context scoped.</summary>
    public static IServiceCollection AddStaffWorkflows(this IServiceCollection services)
    {
        services.AddScoped<IStaffUnitOfWork, StaffUnitOfWork>();
        services.AddScoped<IEmployeePickupRepository, EmployeePickupRepository>();
        services.AddScoped<IEmployeeCollectionRepository, EmployeeCollectionRepository>();
        services.AddScoped<IEmployeeReportingRepository, EmployeeReportingRepository>();
        services.AddScoped<IEmployeeDispatchRepository, EmployeeDispatchRepository>();
        services.AddScoped<DriverJobRepository>();
        services.AddScoped<IDriverJobRepository>(sp => sp.GetRequiredService<DriverJobRepository>());
        services.AddScoped<IDriverDeliveryRepository, DriverDeliveryRepository>();
        services.AddScoped<IDriverReportingRepository, DriverReportingRepository>();
        services.AddScoped<IDriverNoticeRepository, DriverNoticeRepository>();
        services.AddScoped<EmployeePickupService>();
        services.AddScoped<IEmployeeCollectionService, EmployeeCollectionService>();
        services.AddScoped<EmployeeReportingService>();
        services.AddScoped<EmployeeDispatchService>();
        services.AddScoped<EmployeeDailyPlanService>();
        services.AddScoped<Retrack.API.Repositories.EmployeeDailyPlanRepository>();
        services.AddScoped<DriverJobService>();
        services.AddScoped<DriverDeliveryService>();
        services.AddScoped<DriverReportingService>();
        services.AddScoped<DriverNoticeService>();
        return services;
    }
}
