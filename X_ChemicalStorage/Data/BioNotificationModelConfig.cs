using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.Data
{
    public static class BioNotificationModelConfig
    {
        /// استدعيها في OnModelCreating جنب builder.ConfigureBioIoT();
        public static void ConfigureBioNotifications(this ModelBuilder builder)
        {
            builder.Entity<Bio_AlarmContact>(e =>
            {
                e.HasIndex(x => new { x.IsActive, x.Tier });
            });

            builder.Entity<Bio_AlarmNotification>(e =>
            {
                e.HasIndex(x => new { x.Status, x.NextAttemptAt });   // طابور الإرسال
                e.HasIndex(x => x.AlarmId);
                e.HasIndex(x => new { x.FreezerId, x.CreatedAt });
            });
        }
    }
}