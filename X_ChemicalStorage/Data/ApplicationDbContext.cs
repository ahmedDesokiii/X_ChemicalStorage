using Microsoft.EntityFrameworkCore;

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
            //builder.UseCollation("Arabic_CI_AS");

            builder.Entity<ApplicationUser>().ToTable("ERP_WebUsers");
            builder.Entity<ApplicationRole>().ToTable("ERP_Roles");
            builder.Entity<IdentityUserRole<int>>().ToTable("ERP_UserRoles");
            builder.Entity<IdentityUserClaim<int>>().ToTable("ERP_UserClaims");
            builder.Entity<IdentityUserLogin<int>>().ToTable("ERP_UserLogins");
            builder.Entity<IdentityRoleClaim<int>>().ToTable("ERP_RoleClaims");
            builder.Entity<IdentityUserToken<int>>().ToTable("ERP_UserToken");


            // Configure the composite primary key for the join entity
            builder.Entity<SupplierLot>()
                .HasKey(sl => new { sl.SupplierId, sl.LotId });

            // Define the relationship from Supplier to the join entity
            builder.Entity<SupplierLot>()
                .HasOne(sl => sl.Supplier)
                .WithMany(s => s.SupplierLots)
                .HasForeignKey(sl => sl.SupplierId);

            // Define the relationship from Lot to the join entity
            builder.Entity<SupplierLot>()
                .HasOne(sl => sl.Lot)
                .WithMany(l => l.SupplierLots)
                .HasForeignKey(sl => sl.LotId);

        }
        public virtual DbSet<Supplier> Suppliers { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Unit> Units { get; set; }
        public virtual DbSet<Location> Locations { get; set; }
        public virtual DbSet<Lot> Lots { get; set; }
        //public virtual DbSet<Substance> Substances { get; set; }
        public virtual DbSet<Item> Items { get; set; }
        public virtual DbSet<SupplierLot> SupplierLots { get; set; } // DbSet for the join table

        public virtual DbSet<ItemTransaction> ItemTransactions { get; set; }
        public virtual DbSet<ManufacuterCompany> ManufacuterCompanies { get; set; }

       
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
    }
}
