using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Bio_FreezerReading
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(50)]
        public string FreezerId { get; set; } = null!;

        public decimal Temperature { get; set; }

        [MaxLength(20)]
        public string? DoorStatus { get; set; }

        [MaxLength(20)]
        public string? PowerStatus { get; set; }

        public bool IsOnline { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(4000)]
        public string? RawPayload { get; set; }

        // ===== جديد =====
        public decimal? SetTemperature { get; set; }        // الحرارة المضبوطة
        public decimal? AmbientTemperature { get; set; }    // حرارة الغرفة
        public bool? HighCompressorRunning { get; set; }    // High Temperature Compressor
        public bool? CryoCompressorRunning { get; set; }    // Cryogenic Compressor

        [ForeignKey(nameof(FreezerId))]
        public Bio_Freezer Freezer { get; set; } = null!;
    }
}