using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Retrack.API.Data;
using Retrack.API.Middleware;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════
// 1. DATABASE
// ══════════════════════════════════════════════════════════
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ══════════════════════════════════════════════════════════
// 2. REPOSITORIES (Data Access Layer)
// ══════════════════════════════════════════════════════════
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
// TODO: Register other repositories
// builder.Services.AddScoped<IPickupRequestRepository, PickupRequestRepository>();
// builder.Services.AddScoped<IDepotRepository, DepotRepository>();
// builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
// builder.Services.AddScoped<IBatchOrderRepository, BatchOrderRepository>();
// builder.Services.AddScoped<ITransportJobRepository, TransportJobRepository>();
// builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// ══════════════════════════════════════════════════════════
// 3. SERVICES (Business Logic Layer)
// ══════════════════════════════════════════════════════════
// builder.Services.AddScoped<IAuthService, AuthService>();
// builder.Services.AddScoped<ISellerService, SellerService>();
// builder.Services.AddScoped<IDepotService, DepotService>();
// builder.Services.AddScoped<IEmployeePickupService, EmployeePickupService>();
// builder.Services.AddScoped<IDriverTransportService, DriverTransportService>();
// builder.Services.AddScoped<IFactoryMarketService, FactoryMarketService>();
// builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ══════════════════════════════════════════════════════════
// 4. JWT AUTHENTICATION
// ══════════════════════════════════════════════════════════
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
    };
});

// ══════════════════════════════════════════════════════════
// 5. OTHER SERVICES
// ══════════════════════════════════════════════════════════
builder.Services.AddHttpClient();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ══════════════════════════════════════════════════════════
// MIDDLEWARE PIPELINE
// ══════════════════════════════════════════════════════════
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
