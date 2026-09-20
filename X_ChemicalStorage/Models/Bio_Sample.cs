using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.Models
{
    public class Bio_Sample
    {
        [Key]
        [MaxLength(50)]
        public string Id { get; set; } = null!;              // SMP-2026-00001

        [MaxLength(100)]
        public string? ExternalId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        [MaxLength(100)]
        public string? SampleType { get; set; }              // Plasma, Serum, DNA...

        [MaxLength(100)]
        public string? PatientRef { get; set; }

        [MaxLength(50)]
        public string? ProjectCode { get; set; }

        public DateTime? CollectedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public SampleLifecycleStatus Status { get; set; } = SampleLifecycleStatus.Draft;

        // Current location (denormalized for speed)
        [MaxLength(180)]
        public string? CurrentTubeId { get; set; }

        [MaxLength(150)]
        public string? CurrentBoxId { get; set; }

        [MaxLength(5)]
        public string? CurrentPosition { get; set; }

        [MaxLength(50)]
        public string? CurrentFreezerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public ICollection<Bio_SampleMovement> Movements { get; set; } = new List<Bio_SampleMovement>();
    }
}
