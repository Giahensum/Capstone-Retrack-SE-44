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
        modelBuilder.Entity<Factory>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<Factory>().Property(x => x.AcceptedMaterials)
            .HasConversion(v => string.Join(",", v.Select(x => x.ToString())),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => Enum.Parse<MaterialType>(x)).ToArray())
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<MaterialType[]>(
                (a, b) => a!.SequenceEqual(b!),
                a => a.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)), a => a.ToArray()));
        modelBuilder.Entity<InventoryBatch>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<InventoryBatch>().HasOne<Factory>().WithMany().HasForeignKey(x => x.TargetFactoryId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BatchOrder>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<BatchOrder>().HasIndex(x => x.BatchId).IsUnique();
        modelBuilder.Entity<Partnership>().HasIndex(x => new { x.FactoryId, x.DepotId }).IsUnique();
        modelBuilder.Entity<WeightVerification>().HasIndex(x => x.BatchOrderId).IsUnique();
        modelBuilder.Entity<WeightTicket>().HasIndex(x => x.BatchOrderId).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(x => x.BatchOrderId).IsUnique();
        modelBuilder.Entity<EprCertificate>().HasIndex(x => x.BatchOrderId).IsUnique();
        modelBuilder.Entity<EprCertificate>().HasIndex(x => x.CertificateCode).IsUnique();
        modelBuilder.Entity<PlatformFeeLog>().HasIndex(x => x.BatchOrderId).IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<InventoryBatch>().Where(e => e.State == EntityState.Modified))
            entry.Entity.Version = Guid.NewGuid();
        foreach (var entry in ChangeTracker.Entries<BatchOrder>().Where(e => e.State == EntityState.Modified))
            entry.Entity.Version = Guid.NewGuid();
        return base.SaveChangesAsync(cancellationToken);
    }
}
