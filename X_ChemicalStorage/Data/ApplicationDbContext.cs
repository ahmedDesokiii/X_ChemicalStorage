using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        
         protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>().ToTable("ERP_WebUsers");
            builder.Entity<ApplicationRole>().ToTable("ERP_Roles");
            builder.Entity<IdentityUserRole<int>>().ToTable("ERP_UserRoles");
            builder.Entity<IdentityUserClaim<int>>().ToTable("ERP_UserClaims");
            builder.Entity<IdentityUserLogin<int>>().ToTable("ERP_UserLogins");
            builder.Entity<IdentityRoleClaim<int>>().ToTable("ERP_RoleClaims");
            builder.Entity<IdentityUserToken<int>>().ToTable("ERP_UserToken");



            // ================= SubCategory relationships (NEW) =================

            // Category 1 : N SubCategory
            builder.Entity<SubCategory>()
                .HasOne(sc => sc.Category)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(sc => sc.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // never let a Category delete cascade into SubCategories

            // SubCategory 1 : N Item  (nullable FK — existing Items may have none yet)
            builder.Entity<Item>()
                .HasOne(i => i.SubCategory)
                .WithMany(sc => sc.Items)
                .HasForeignKey(i => i.SubCategoryId)
                .OnDelete(DeleteBehavior.Restrict); // never let a SubCategory delete cascade into Items

            // Helpful index for the cascading dropdown query (WHERE CategoryId = ...)
            builder.Entity<SubCategory>()
                .HasIndex(sc => sc.CategoryId);

            builder.Entity<Item>()
                .HasIndex(i => i.SubCategoryId);

            // ====================================================================


            // منع تخزين أكتر من عينة في نفس المكان بالظبط
            builder.Entity<Lot>()
                .HasIndex(l => new { l.BoxId, l.RowIndex, l.ColumnIndex })
                .IsUnique();

            // ✅ منع الـ Cascade المتضارب - نحول كل العلاقات دي لـ Restrict
            builder.Entity<Item>()
                .HasOne(i => i.StorageUnitType)
                .WithMany(t => t.Items)
                .HasForeignKey(i => i.StorageUnitTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StorageUnit>()
                .HasOne(u => u.Room)
                .WithMany(r => r.StorageUnits)
                .HasForeignKey(u => u.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StorageUnit>()
                .HasOne(u => u.StorageUnitType)
                .WithMany(t => t.StorageUnits)
                .HasForeignKey(u => u.StorageUnitTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Shelf>()
                .HasOne(s => s.StorageUnit)
                .WithMany(u => u.Shelves)
                .HasForeignKey(s => s.StorageUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Box>()
                .HasOne(b => b.Shelf)
                .WithMany(s => s.Boxes)
                .HasForeignKey(b => b.ShelfId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Lot>()
                .HasOne(l => l.Box)
                .WithMany(b => b.Lots)
                .HasForeignKey(l => l.BoxId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Lot>()
                .HasOne(l => l.Item)
                .WithMany(i => i.Lots)
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupplierLot>()
                .HasKey(sl => new { sl.SupplierId, sl.LotId });

            // Seed لأنواع الوحدات الثابتة
            builder.Entity<StorageUnitType>().HasData(
                new StorageUnitType { Id = 1, Name = "Freezer-20°C", TemperatureValue = -20 },
                new StorageUnitType { Id = 2, Name = "Freezer-80°C", TemperatureValue = -80 },
                new StorageUnitType { Id = 3, Name = "Freezer-150°C", TemperatureValue = -150 },
                new StorageUnitType { Id = 4, Name = "2°C-8°C", TemperatureValue = 4 },
                new StorageUnitType { Id = 5, Name = "Liquid Nitrogen Tank", TemperatureValue = null },
                new StorageUnitType { Id = 6, Name = "Room Temp", TemperatureValue = null }
            );

            // Bio Bank Entities Configuration
            // ===== Bio_Freezer =====
            builder.Entity<Bio_Freezer>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Type).HasConversion<string>();
                e.Property(x => x.Status).HasConversion<string>();
                e.HasIndex(x => x.Barcode);
            });

            // ===== Bio_Shelf =====
            builder.Entity<Bio_Shelf>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Bio_Freezer)
                    .WithMany(f => f.Bio_Shelves)
                    .HasForeignKey(x => x.FreezerId)
                    .OnDelete(DeleteBehavior.Cascade);   // Freezer → Shelf : Cascade
            });

            // ===== Bio_Rack =====
            builder.Entity<Bio_Rack>(e =>
            {
                e.HasKey(x => x.Id);

                // Freezer → Rack : Cascade
                e.HasOne(x => x.Bio_Freezer)
                    .WithMany(f => f.Bio_Racks)
                    .HasForeignKey(x => x.FreezerId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Shelf → Rack : NO ACTION (هذا هو الإصلاح المهم)
                e.HasOne(x => x.Bio_Shelf)
                    .WithMany(s => s.Bio_Racks)
                    .HasForeignKey(x => x.ShelfId)
                    .OnDelete(DeleteBehavior.Restrict)   // ← غيّر من Cascade إلى Restrict
                    .IsRequired(false);
            });

            // ===== Bio_Drawer =====
            builder.Entity<Bio_Drawer>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Bio_Rack)
                    .WithMany(r => r.Bio_Drawers)
                    .HasForeignKey(x => x.RackId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== Bio_Box =====
            builder.Entity<Bio_Box>(e =>
            {
                e.HasKey(x => x.Id);

                // Drawer → Box : Cascade
                e.HasOne(x => x.Bio_Drawer)
                    .WithMany(d => d.Bio_Boxes)
                    .HasForeignKey(x => x.DrawerId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(false);

                // Rack → Box : Restrict (عشان نتجنب multiple cascade paths)
                e.HasOne(x => x.Bio_Rack)
                    .WithMany(r => r.Bio_Boxes)
                    .HasForeignKey(x => x.RackId)
                    .OnDelete(DeleteBehavior.Restrict)   // ← مهم
                    .IsRequired(false);
            });

            // ===== Bio_Tube =====
            builder.Entity<Bio_Tube>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Status).HasConversion<string>();
                e.HasIndex(x => new { x.BoxId, x.Position }).IsUnique();
                e.HasIndex(x => x.SampleId);

                e.HasOne(x => x.Bio_Box)
                    .WithMany(b => b.Bio_Tubes)
                    .HasForeignKey(x => x.BoxId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<Bio_Tube>()
            .HasIndex(t => t.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL"); // adjust filter syntax for your DB provider

            // Bio_Sample
            builder.Entity<Bio_Sample>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Status).HasConversion<string>();
                e.HasIndex(x => x.ExternalId);
                e.HasIndex(x => x.PatientRef);
                e.HasIndex(x => x.ProjectCode);
                e.HasIndex(x => x.CurrentTubeId);
                e.HasIndex(x => x.Status);
            });

            // Bio_SampleMovement
            builder.Entity<Bio_SampleMovement>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Type).HasConversion<string>();
                e.HasIndex(x => x.SampleId);
                e.HasIndex(x => x.PerformedAt);

                e.HasOne(x => x.Bio_Sample)
                    .WithMany(s => s.Movements)
                    .HasForeignKey(x => x.SampleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<StorageUnitType> StorageUnitTypes { get; set; }
        public DbSet<StorageUnit> StorageUnits { get; set; }
        public DbSet<Shelf> Shelves { get; set; }
        public DbSet<Box> Boxes { get; set; }
        public virtual DbSet<Supplier> Suppliers { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<SubCategory> SubCategories { get; set; }   // NEW
        public virtual DbSet<Unit> Units { get; set; }
        public virtual DbSet<Location> Locations { get; set; }
        public virtual DbSet<Lot> Lots { get; set; }
        public virtual DbSet<Item> Items { get; set; }
        public virtual DbSet<SupplierLot> SupplierLots { get; set; }
        public virtual DbSet<ItemTransaction> ItemTransactions { get; set; }
        public virtual DbSet<LotTransaction> LotTransactions { get; set; }
        public virtual DbSet<ManufacuterCompany> ManufacuterCompanies { get; set; }
        // Bio Bank Entities
        public DbSet<Bio_Freezer> Bio_Freezers => Set<Bio_Freezer>();
        public DbSet<Bio_Shelf> Bio_Shelves => Set<Bio_Shelf>();
        public DbSet<Bio_Rack> Bio_Racks => Set<Bio_Rack>();
        public DbSet<Bio_Drawer> Bio_Drawers => Set<Bio_Drawer>();
        public DbSet<Bio_Box> Bio_Boxes => Set<Bio_Box>();
        public DbSet<Bio_Tube> Bio_Tubes => Set<Bio_Tube>();

        // Bio Samples 

        public DbSet<Bio_Sample> Bio_Samples => Set<Bio_Sample>();
        public DbSet<Bio_SampleMovement> Bio_SampleMovements => Set<Bio_SampleMovement>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
        
    }
}
