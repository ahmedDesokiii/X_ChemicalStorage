using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.Data
{
    public static class BioIoTModelConfig
    {
        /// استدعيها في آخر OnModelCreating: builder.ConfigureBioIoT();
        public static void ConfigureBioIoT(this ModelBuilder builder)
        {
            builder.Entity<Bio_FreezerSettings>(e =>
            {
                e.HasKey(x => x.FreezerId);
                e.Property(x => x.HighTempLimit).HasPrecision(6, 2);
                e.Property(x => x.LowTempLimit).HasPrecision(6, 2);
                e.HasOne(x => x.Freezer).WithOne()
                 .HasForeignKey<Bio_FreezerSettings>(x => x.FreezerId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Bio_FreezerReading>(e =>
            {
                e.Property(x => x.Temperature).HasPrecision(6, 2);
                e.Property(x => x.SetTemperature).HasPrecision(6, 2);
                e.Property(x => x.AmbientTemperature).HasPrecision(6, 2);
                e.HasIndex(x => new { x.FreezerId, x.Timestamp });
                // ممنوع حذف فريزر عنده قراءات (سجل تدقيق)
                e.HasOne(x => x.Freezer).WithMany()
                 .HasForeignKey(x => x.FreezerId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Bio_FreezerAlarm>(e =>
            {
                e.Property(x => x.PeakValue).HasPrecision(6, 2);
                e.HasIndex(x => new { x.FreezerId, x.IsCleared });
                e.HasOne(x => x.Freezer).WithMany()
                 .HasForeignKey(x => x.FreezerId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Bio_IoTDevice>(e =>
            {
                e.HasIndex(x => x.CollectorCode).IsUnique();
            });

            builder.Entity<Bio_Freezer>(e =>
            {
                e.Property(x => x.CurrentTemperature).HasPrecision(6, 2);
            });
        }
    }

    /// يمنع تعديل أو حذف القراءات (GLP/GMP audit trail).
    public class ReadingImmutabilityInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> result)
        {
            Check(e.Context);
            return base.SavingChanges(e, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Check(e.Context);
            return base.SavingChangesAsync(e, result, ct);
        }

        private static void Check(DbContext? ctx)
        {
            if (ctx == null) return;
            var bad = ctx.ChangeTracker.Entries<Bio_FreezerReading>()
                .Any(x => x.State == EntityState.Modified || x.State == EntityState.Deleted);
            if (bad)
                throw new InvalidOperationException("Freezer readings are immutable (audit trail): update/delete is not allowed.");
        }
    }
}