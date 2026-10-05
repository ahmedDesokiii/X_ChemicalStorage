using System.ComponentModel.DataAnnotations;

namespace X_ChemicalStorage.Models
{
    /// سجل تدقيق لتغيير إعدادات الإنذار (حدود/تأخير/وضع التشغيل). لا يُعدَّل ولا يُحذف.
    public class Bio_FreezerSettingsAudit
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(50)]
        public string FreezerId { get; set; } = null!;

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string ChangedBy { get; set; } = "";

        [Required, MaxLength(50)]
        public string Field { get; set; } = null!;

        [MaxLength(50)]
        public string? OldValue { get; set; }

        [MaxLength(50)]
        public string? NewValue { get; set; }
    }
}