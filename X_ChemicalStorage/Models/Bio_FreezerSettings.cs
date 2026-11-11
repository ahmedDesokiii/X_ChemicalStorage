using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.Models
{
    /// <summary>
    /// إعدادات المراقبة والإنذار لكل فريزر (صف واحد لكل فريزر).
    /// </summary>
    public class Bio_FreezerSettings
    {
        [Key, MaxLength(50)]
        public string FreezerId { get; set; } = null!;

        // ===== حدود الحرارة (°C) =====
        public decimal HighTempLimit { get; set; }   // إنذار لو القراءة أعلى منها
        public decimal LowTempLimit { get; set; }    // إنذار لو القراءة أقل منها

        // ===== التوقيتات (بالدقائق) =====
        public int TempAlarmDelayMinutes { get; set; } = 10;  // مدة بقاء الحرارة خارج الحد قبل الإنذار
        public int DoorOpenMinutes { get; set; } = 5;         // مهلة الباب المفتوح
        public int OfflineAfterMinutes { get; set; } = 12;    // انقطاع الرسايل = Offline (بوابات هاير بتبعت الحرارة كل 5 دقايق)
        public int EscalationMinutes { get; set; } = 10;      // مفيش استلام => مستوى 2

        // ===== وضع قيد التشغيل (Commissioning) =====
        // true: الإنذارات معطّلة (الفريزر لسه بيتجهز). يتقفل تلقائياً أول ما القراءة تدخل النطاق.
        public bool IsCommissioning { get; set; } = true;
        public DateTime? CommissioningEndedAt { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        [ForeignKey(nameof(FreezerId))]
        public Bio_Freezer Freezer { get; set; } = null!;

        /// <summary>القيم الافتراضية حسب نوع الفريزر.</summary>
        public static Bio_FreezerSettings CreateDefault(string freezerId, FreezerType type) => new()
        {
            FreezerId = freezerId,
            HighTempLimit = type == FreezerType.Minus150 ? -140m : -70m,
            LowTempLimit = type == FreezerType.Minus150 ? -160m : -90m,
            IsCommissioning = true,
            UpdatedAt = DateTime.UtcNow
        };
    }
}