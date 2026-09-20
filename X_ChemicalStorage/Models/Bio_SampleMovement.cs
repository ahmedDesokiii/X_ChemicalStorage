using System.ComponentModel.DataAnnotations.Schema;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.Models
{
    public class Bio_SampleMovement
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string SampleId { get; set; } = null!;

        public MovementType Type { get; set; }

        [MaxLength(180)]
        public string? FromTubeId { get; set; }

        [MaxLength(180)]
        public string? ToTubeId { get; set; }

        [MaxLength(5)]
        public string? FromPosition { get; set; }

        [MaxLength(5)]
        public string? ToPosition { get; set; }

        [MaxLength(150)]
        public string? FromBoxId { get; set; }

        [MaxLength(150)]
        public string? ToBoxId { get; set; }

        [MaxLength(300)]
        public string? Reason { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? PerformedBy { get; set; }

        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SampleId))]
        public Bio_Sample Bio_Sample { get; set; } = null!;
    }
}
