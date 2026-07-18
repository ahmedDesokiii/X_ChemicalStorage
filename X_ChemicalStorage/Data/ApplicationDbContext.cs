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
        }
        //public DbSet<PrintSetting> PrintSettings { get; set; }


        public DbSet<Room> Rooms { get; set; }
        public DbSet<StorageUnitType> StorageUnitTypes { get; set; }
        public DbSet<StorageUnit> StorageUnits { get; set; }
        public DbSet<Shelf> Shelves { get; set; }
        public DbSet<Box> Boxes { get; set; }

       
        public virtual DbSet<Supplier> Suppliers { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Unit> Units { get; set; }
        public virtual DbSet<Location> Locations { get; set; }
        public virtual DbSet<Lot> Lots { get; set; }
        //public virtual DbSet<Substance> Substances { get; set; }
        public virtual DbSet<Item> Items { get; set; }
        public virtual DbSet<SupplierLot> SupplierLots { get; set; } // DbSet for the join table

        public virtual DbSet<ItemTransaction> ItemTransactions { get; set; }
        public virtual DbSet<LotTransaction> LotTransactions { get; set; }
        public virtual DbSet<ManufacuterCompany> ManufacuterCompanies { get; set; }

       
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
        
    }
}
