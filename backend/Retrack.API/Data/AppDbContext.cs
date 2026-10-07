using Microsoft.EntityFrameworkCore;
using Retrack.API.Models;

namespace Retrack.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets
        public DbSet<SystemConfig> SystemConfigs { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Depot> Depots { get; set; }
        public DbSet<Factory> Factories { get; set; }
        public DbSet<DepotStaff> DepotStaffs { get; set; }
        public DbSet<PickupRequest> PickupRequests { get; set; }
        public DbSet<PickupRequestItem> PickupRequestItems { get; set; }
        public DbSet<SellerDepotReview> SellerDepotReviews { get; set; }
        public DbSet<FactoryDemand> FactoryDemands { get; set; }
        public DbSet<FactoryDepotPartnership> FactoryDepotPartnerships { get; set; }
        public DbSet<InventoryBatch> InventoryBatches { get; set; }
        public DbSet<TransportJob> TransportJobs { get; set; }
        public DbSet<BatchQualityCheck> BatchQualityChecks { get; set; }
        public DbSet<FactoryDepotReview> FactoryDepotReviews { get; set; }
        public DbSet<PlatformTransaction> PlatformTransactions { get; set; }
        // Admin quản lý bảng giá dùng chung; Factory chỉ đọc để tham khảo khi giao dịch.
        public DbSet<MarketPrice> MarketPrices { get; set; }
        // Admin DbSets
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PlatformInvoice> PlatformInvoices { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Các model scaffold của kiến trúc cũ không thuộc schema dùng chung.
            // Khai báo tập trung để EF không tự sinh lại các bảng PascalCase.
            modelBuilder.Ignore<Seller>();
            modelBuilder.Ignore<DepotEmployee>();
            modelBuilder.Ignore<Driver>();
            modelBuilder.Ignore<PickupRequestImage>();
            modelBuilder.Ignore<BatchImage>();
            modelBuilder.Ignore<BatchOrder>();
            modelBuilder.Ignore<TransportTrackingLog>();
            modelBuilder.Ignore<WeightVerification>();
            modelBuilder.Ignore<WeightTicket>();
            modelBuilder.Ignore<Partnership>();
            modelBuilder.Ignore<PlatformFeeLog>();
            modelBuilder.Ignore<EprCertificate>();

            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
                modelBuilder.HasSequence<long>("depot_batch_number");
            modelBuilder.Entity<InventoryBatch>().HasIndex(b => b.Code).IsUnique();

            // User - unique email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<PlatformTransaction>()
                .HasIndex(t => new { t.SourceType, t.SourceId }).IsUnique();
            // PickupRequest - multiple FK to User
            modelBuilder.Entity<PickupRequest>()
                .HasOne(p => p.Seller)
                .WithMany(u => u.PickupRequests)
                .HasForeignKey(p => p.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PickupRequest>()
                .HasOne(p => p.AcceptedCollector)
                .WithMany()
                .HasForeignKey(p => p.AcceptedCollectorId)
                .OnDelete(DeleteBehavior.SetNull);

            // FactoryDepotPartnership - unique constraint
            modelBuilder.Entity<FactoryDepotPartnership>()
                .HasIndex(p => new { p.DepotId, p.FactoryId }).IsUnique();

            modelBuilder.Entity<InventoryBatch>()
                .HasOne(batch => batch.DirectOfferFactory)
                .WithMany()
                .HasForeignKey(batch => batch.DirectOfferFactoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FactoryDemand>().Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

            modelBuilder.Entity<MarketPrice>()
                .Property(p => p.MaterialType).HasConversion<string>().HasMaxLength(100);

            // TransportJob - unique batch_id
            modelBuilder.Entity<TransportJob>()
                .HasIndex(t => t.BatchId).IsUnique();

            // BatchQualityCheck - unique batch_id
            modelBuilder.Entity<BatchQualityCheck>()
                .HasIndex(b => b.BatchId).IsUnique();

            // Depot -> User (owner) - restrict delete
            modelBuilder.Entity<Depot>()
                .HasOne(d => d.Owner)
                .WithMany(u => u.OwnedDepots)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Factory -> User (owner) - restrict delete
            modelBuilder.Entity<Factory>()
                .HasOne(f => f.Owner)
                .WithMany(u => u.OwnedFactories)
                .HasForeignKey(f => f.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // PlatformInvoice -> User (payer) - restrict delete, one invoice per payer per period
            modelBuilder.Entity<PlatformInvoice>()
                .HasOne(i => i.Payer)
                .WithMany()
                .HasForeignKey(i => i.PayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlatformInvoice>()
                .HasIndex(i => new { i.PayerId, i.PeriodYear, i.PeriodMonth }).IsUnique();

            // AuditLog -> User (nullable, keep log if user is deleted)
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Notification -> User
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Schema chính thức dùng snake_case. Chặn entity mới vô tình sinh bảng PascalCase
            // (ví dụ Users, BatchOrders) như DbContext scaffold đời đầu.
            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                var invalidTableNames = modelBuilder.Model.GetEntityTypes()
                    .Select(entity => entity.GetTableName())
                    .Where(tableName => tableName is not null && tableName != tableName.ToLowerInvariant())
                    .Distinct()
                    .OrderBy(tableName => tableName)
                    .ToArray();

                if (invalidTableNames.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Tên bảng EF phải dùng snake_case. Bảng không hợp lệ: {string.Join(", ", invalidTableNames)}");
                }
            }

            // Seed data
            modelBuilder.Entity<SystemConfig>().HasData(new SystemConfig
            {
                ConfigKey = "PLATFORM_FEE_PERCENTAGE",
                ConfigValue = "5.00",
                Description = "Phí nền tảng mặc định 5%",
                UpdatedAt = new DateTime(2026, 9, 20, 7, 49, 15, 238, DateTimeKind.Utc).AddTicks(399)
            });
        }
    }
}
