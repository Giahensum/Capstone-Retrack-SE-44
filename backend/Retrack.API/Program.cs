using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Retrack.API.Data;
using Retrack.API.Middleware;
using Retrack.API.Repositories;
using Retrack.API.Services;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // EventLog trên Windows có thể từ chối ghi và che mất lỗi trả về từ API local.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    Retrack.API.Helpers.LocalEnvironment.Load(Path.Combine(builder.Environment.ContentRootPath, ".env"));
    // Nạp lại biến môi trường sau .env; tham số dòng lệnh vẫn có ưu tiên cao nhất.
    builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
}

// ===== DATABASE =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== AUTHENTICATION (JWT) =====
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                {
                    context.Fail("Invalid identity.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, context.HttpContext.RequestAborted);
                var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
                if (user == null || !user.IsActive || user.Role != role ||
                    ((role is "DEPOT_EMPLOYEE" or "DRIVER") && !await db.DepotStaffs.AnyAsync(
                        s => s.UserId == id && s.StaffType == role && s.IsActive, context.HttpContext.RequestAborted)))
                    context.Fail("Account or depot membership is inactive.");
            }
        };
    });

builder.Services.AddAuthorization();

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
                builder.Configuration["Frontend:Url"] ?? "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// ===== DEPENDENCY INJECTION =====
// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPickupRequestRepository, PickupRequestRepository>();
builder.Services.AddScoped<IMarketPriceRepository, MarketPriceRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IPlatformInvoiceRepository, PlatformInvoiceRepository>();

builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotOwnerRepository, DepotOwnerRepository>();

builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotPaymentReadRepository, DepotPaymentReadRepository>();

builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotUnitOfWork, DepotUnitOfWork>();
builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotPaymentRepository, DepotPaymentRepository>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IDepotPaymentService, Retrack.API.Services.Depot.DepotPaymentService>();

builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotBatchRepository, DepotBatchRepository>();

builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotReportRepository, DepotReportRepository>();

builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotPartnershipRepository, DepotPartnershipRepository>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IDepotPartnershipService, Retrack.API.Services.Depot.DepotPartnershipService>();

// Services - Admin
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStaffProfileRepository, StaffProfileRepository>();
builder.Services.AddScoped<Retrack.API.Services.Staff.IStaffProfileService, Retrack.API.Services.Staff.StaffProfileService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.ICloudinaryService, Retrack.API.Services.Shared.CloudinaryService>();
builder.Services.AddScoped<IPickupService, PickupService>();
builder.Services.AddScoped<Retrack.API.Services.Employee.IEmployeeCollectionService, Retrack.API.Services.Employee.EmployeeCollectionService>();
builder.Services.AddScoped<Retrack.API.Services.Employee.EmployeeReportingService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IDepotService, Retrack.API.Services.Depot.DepotService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IInventoryService, Retrack.API.Services.Depot.InventoryService>();
builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotInventoryRepository, DepotInventoryRepository>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IDepotProofService, Retrack.API.Services.Depot.DepotProofService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IBatchService, Retrack.API.Services.Depot.BatchService>();
builder.Services.AddScoped<Retrack.API.Repositories.Interfaces.IDepotStaffRepository, DepotStaffRepository>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IStaffService, Retrack.API.Services.Depot.StaffService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IDepotReportService, Retrack.API.Services.Depot.DepotReportService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.INotificationService, Retrack.API.Services.Shared.NotificationService>();
builder.Services.AddScoped<Retrack.API.Services.Driver.DriverJobService>();
builder.Services.AddScoped<Retrack.API.Services.Driver.DriverReportingService>();
builder.Services.AddScoped<Retrack.API.Services.Driver.DriverDeliveryService>();


// Services - Factory
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryDashboardService, Retrack.API.Services.Factory.FactoryDashboardService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryDemandService, Retrack.API.Services.Factory.FactoryDemandService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryMarketService, Retrack.API.Services.Factory.FactoryMarketService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryOrderService, Retrack.API.Services.Factory.FactoryOrderService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryPartnerService, Retrack.API.Services.Factory.FactoryPartnerService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryProfileService, Retrack.API.Services.Factory.FactoryProfileService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IQCService, Retrack.API.Services.Factory.FactoryQCService>();
builder.Services.AddScoped<Retrack.API.Services.Factory.FactoryAttachmentService>();

// ===== CONTROLLERS & SWAGGER =====
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ReTrack API",
        Version = "v1",
        Description = "ReTrack — Recycling Tracking Platform API"
    });

    // JWT Auth in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    // Resolve duplicate action conflicts (e.g. same route registered twice)
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
    c.CustomOperationIds(e => $"{e.ActionDescriptor.RouteValues["controller"]}_{e.ActionDescriptor.RouteValues["action"]}_{e.HttpMethod}");
});

// ===== BUILD =====
var app = builder.Build();

// ===== MIDDLEWARE PIPELINE =====
app.UseExceptionHandling(); // Global exception handler

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ReTrack API v1");
        c.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== DB-FIRST: app KHÔNG tự migrate; schema nằm ở db/depot ower/retrack-system.sql =====
// Tạo/cập nhật schema bằng script SQL đã review trước khi khởi động ứng dụng.
// Chỉ seed dữ liệu khi chủ động bật Database__Initialize trong Development.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DataSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:Initialize"))
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try {
        var ownerId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var additionalDepots = new[]
        {
            new Retrack.API.Models.Depot { Id = Guid.Parse("00000000-0000-0000-0000-000000000101"), OwnerId = ownerId, Name = "Vựa Phế Liệu FPT City", Address = "Khu vực quảng trường FPT City, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)", Latitude = 15.98144m, Longitude = 108.26106m, Rating = 4.8m },
            new Retrack.API.Models.Depot { Id = Guid.Parse("00000000-0000-0000-0000-000000000102"), OwnerId = ownerId, Name = "Kho Thu Mua Hòa Hải", Address = "Khu đô thị FPT, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)", Latitude = 15.9832468m, Longitude = 108.2520905m, Rating = 4.2m },
            new Retrack.API.Models.Depot { Id = Guid.Parse("00000000-0000-0000-0000-000000000103"), OwnerId = ownerId, Name = "Điểm Thu Gom Ngũ Hành Sơn", Address = "Khu vực đường Huyền Trân Công Chúa, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)", Latitude = 16.0043044m, Longitude = 108.2635270m, Rating = 4.5m },
            new Retrack.API.Models.Depot { Id = Guid.Parse("00000000-0000-0000-0000-000000000104"), OwnerId = ownerId, Name = "Kho Phế Liệu Mỹ An", Address = "Khu vực Mỹ An, phường Ngũ Hành Sơn, Đà Nẵng (địa điểm demo)", Latitude = 16.0250950m, Longitude = 108.2595335m, Rating = 4.9m }
        };
        var demoIds = additionalDepots.Select(d => d.Id).ToArray();
        var existingIds = await db.Depots.Where(d => demoIds.Contains(d.Id)).Select(d => d.Id).ToListAsync();
        var missingDepots = additionalDepots.Where(d => !existingIds.Contains(d.Id)).ToArray();
        if (missingDepots.Length > 0)
        {
            db.Depots.AddRange(missingDepots);
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Đã thêm {Count} kho demo tại Đà Nẵng", missingDepots.Length);
        }
    }
    catch (Exception ex) {
        app.Logger.LogError(ex, "SEED MORE DEPOTS FAILED");
    }
}

app.Run();
