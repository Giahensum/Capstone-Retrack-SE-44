using System.Text;
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

// Services - Admin
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPickupService, PickupService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.INotificationService, Retrack.API.Services.Shared.NotificationService>();

// Services - Factory
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryDashboardService, Retrack.API.Services.Factory.FactoryDashboardService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryDemandService, Retrack.API.Services.Factory.FactoryDemandService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryMarketService, Retrack.API.Services.Factory.FactoryMarketService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryOrderService, Retrack.API.Services.Factory.FactoryOrderService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryPartnerService, Retrack.API.Services.Factory.FactoryPartnerService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IFactoryProfileService, Retrack.API.Services.Factory.FactoryProfileService>();
builder.Services.AddScoped<Retrack.API.Services.Interfaces.IQCService, Retrack.API.Services.Factory.FactoryQCService>();

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

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== AUTO MIGRATE & SEED (dev only) =====
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await DataSeeder.SeedAsync(db);
}

app.Run();
