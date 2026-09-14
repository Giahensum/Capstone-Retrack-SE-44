using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Retrack.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── Users & Roles ──
    public DbSet<User> Users => Set<User>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<DepotEmployee> DepotEmployees => Set<DepotEmployee>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Factory> Factories => Set<Factory>();

    // ── Pickup (Seller → Depot) ──
    public DbSet<PickupRequest> PickupRequests => Set<PickupRequest>();
    public DbSet<PickupRequestItem> PickupRequestItems => Set<PickupRequestItem>();
    public DbSet<PickupRequestImage> PickupRequestImages => Set<PickupRequestImage>();

    // ── Inventory & Export (Depot → Factory) ──
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<BatchImage> BatchImages => Set<BatchImage>();
    public DbSet<BatchOrder> BatchOrders => Set<BatchOrder>();

    // ── Transport (Driver) ──
    public DbSet<TransportJob> TransportJobs => Set<TransportJob>();
    public DbSet<TransportTrackingLog> TransportTrackingLogs => Set<TransportTrackingLog>();

    // ── QC & Settlement (Factory) ──
    public DbSet<WeightVerification> WeightVerifications => Set<WeightVerification>();
    public DbSet<WeightTicket> WeightTickets => Set<WeightTicket>();

    // ── Business ──
    public DbSet<Partnership> Partnerships => Set<Partnership>();
    public DbSet<FactoryDemand> FactoryDemands => Set<FactoryDemand>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PlatformFeeLog> PlatformFeeLogs => Set<PlatformFeeLog>();
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<EprCertificate> EprCertificates => Set<EprCertificate>();

    // ── System ──
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Enum → string conversion ──
        modelBuilder.Entity<User>()
            .Property(u => u.Role).HasConversion<string>();

        modelBuilder.Entity<PickupRequest>()
            .Property(p => p.Status).HasConversion<string>();

        modelBuilder.Entity<PickupRequestItem>()
            .Property(p => p.MaterialType).HasConversion<string>();

        modelBuilder.Entity<InventoryBatch>()
            .Property(b => b.MaterialType).HasConversion<string>();

        modelBuilder.Entity<InventoryBatch>()
            .Property(b => b.Status).HasConversion<string>();

        modelBuilder.Entity<TransportJob>()
            .Property(t => t.Status).HasConversion<string>();

        modelBuilder.Entity<Partnership>()
            .Property(p => p.Status).HasConversion<string>();

        // ── Unique indexes ──
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<InventoryBatch>()
            .HasIndex(b => b.BatchCode).IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
