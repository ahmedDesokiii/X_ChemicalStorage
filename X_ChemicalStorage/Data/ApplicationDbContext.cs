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

        }
        public virtual DbSet<Supplier> Suppliers { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
    }
}
