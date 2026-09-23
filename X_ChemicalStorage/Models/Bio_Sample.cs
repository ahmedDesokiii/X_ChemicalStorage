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
        public string? PatientRef { get; set; }               // TODO: confirm with Dalia if this = "Owner/PI" from old form, or add separate field

        [MaxLength(50)]
        public string? ProjectCode { get; set; }

        public DateTime? CollectedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public SampleLifecycleStatus Status { get; set; } = SampleLifecycleStatus.Draft;

        // ===== NEW: blood-sample-specific properties =====
        public BloodType BloodType { get; set; } = BloodType.Unknown;

        [MaxLength(150)]
        public string? FastaAccessCode { get; set; }          // sequence access/tracking code

        public decimal? VolumeMl { get; set; }                 // original volume stored
        public decimal? RemainingVolumeMl { get; set; }         // decreases with each CheckOut/CheckIn cycle

        [MaxLength(100)]
        public string? CollectedByEmployeeId { get; set; }      // who physically drew the sample (≠ CreatedBy, who entered the record)

        // ===== NEW: check-out (sample taken out for testing) tracking =====
        public DateTime? CheckedOutAt { get; set; }

        [MaxLength(100)]
        public string? CheckedOutBy { get; set; }

        [MaxLength(300)]
        public string? CheckOutReason { get; set; }             // e.g. test/experiment name

        public DateTime? ExpectedReturnAt { get; set; }
        public DateTime? ReturnedAt { get; set; }

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