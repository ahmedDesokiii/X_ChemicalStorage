// DTOs/SampleDtos.cs
using System.ComponentModel.DataAnnotations;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.BioBank.DTOs
{
    public class Bio_SampleDto
    {
        public string Id { get; set; } = null!;
        public string? ExternalId { get; set; }
        public string Name { get; set; } = null!;
        public string? SampleType { get; set; }
        public string? PatientRef { get; set; }
        public string? ProjectCode { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Notes { get; set; }
        public SampleLifecycleStatus Status { get; set; }

        // NEW
        public BloodType BloodType { get; set; }
        public string? FastaAccessCode { get; set; }
        public decimal? VolumeMl { get; set; }
        public decimal? RemainingVolumeMl { get; set; }
        public string? CollectedByEmployeeId { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public string? CheckedOutBy { get; set; }
        public string? CheckOutReason { get; set; }
        public DateTime? ExpectedReturnAt { get; set; }
        public DateTime? ReturnedAt { get; set; }

        public string? CurrentTubeId { get; set; }
        public string? CurrentBoxId { get; set; }
        public string? CurrentPosition { get; set; }
        public string? CurrentFreezerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class Bio_SampleMovementDto
    {
        public long Id { get; set; }
        public string SampleId { get; set; } = null!;
        public MovementType Type { get; set; }
        public string? FromTubeId { get; set; }
        public string? ToTubeId { get; set; }
        public string? FromPosition { get; set; }
        public string? ToPosition { get; set; }
        public string? FromBoxId { get; set; }
        public string? ToBoxId { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string? PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; }
    }

    public class StoreSampleDto
    {
        [Required, MaxLength(50)]
        public string SampleId { get; set; } = null!;

        [Required, MaxLength(200)]
        public string Name { get; set; } = null!;

        [MaxLength(100)] public string? ExternalId { get; set; }
        [MaxLength(100)] public string? SampleType { get; set; }
        [MaxLength(100)] public string? PatientRef { get; set; }
        [MaxLength(50)] public string? ProjectCode { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        [MaxLength(500)] public string? Notes { get; set; }

        // NEW — blood-sample-specific, filled from the "Add New Blood Sample Record" form
        public BloodType BloodType { get; set; } = BloodType.Unknown;
        [MaxLength(150)] public string? FastaAccessCode { get; set; }
        public decimal? VolumeMl { get; set; }
        [MaxLength(100)] public string? CollectedByEmployeeId { get; set; }

        // NEW — barcode printed on the physical tube (goes on Bio_Tube, not Bio_Sample)
        [MaxLength(100)] public string? TubeBarcode { get; set; }

        [Required, MaxLength(150)]
        public string BoxId { get; set; } = null!;

        [Required, MaxLength(5)]
        public string Position { get; set; } = null!;   // A05

        [MaxLength(100)]
        public string? PerformedBy { get; set; }
    }

    public class ReserveSampleDto
    {
        [Required, MaxLength(150)]
        public string BoxId { get; set; } = null!;

        [Required, MaxLength(5)]
        public string Position { get; set; } = null!;

        [MaxLength(50)]
        public string? SampleId { get; set; }

        [MaxLength(200)]
        public string? SampleName { get; set; }

        [MaxLength(100)]
        public string? PerformedBy { get; set; }

        [MaxLength(300)]
        public string? Reason { get; set; }
    }

    public class WithdrawSampleDto
    {
        [Required, MaxLength(50)]
        public string SampleId { get; set; } = null!;

        [MaxLength(300)]
        public string? Reason { get; set; }

        [MaxLength(100)]
        public string? PerformedBy { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class TransferSampleDto
    {
        [Required, MaxLength(50)]
        public string SampleId { get; set; } = null!;

        [Required, MaxLength(150)]
        public string ToBoxId { get; set; } = null!;

        [Required, MaxLength(5)]
        public string ToPosition { get; set; } = null!;

        [MaxLength(300)]
        public string? Reason { get; set; }

        [MaxLength(100)]
        public string? PerformedBy { get; set; }
    }

    public class TubeSampleDetailsDto
    {
        public string TubeId { get; set; } = null!;
        public string BoxId { get; set; } = null!;
        public string Position { get; set; } = null!;
        public TubeStatus TubeStatus { get; set; }
        public Bio_SampleDto? Sample { get; set; }
    }

    // ===== NEW: check-out / check-in workflow =====

    /// <summary>
    /// Take a stored sample out for an experiment/test.
    /// The tube's position stays locked (TubeStatus.CheckedOut) — NOT freed —
    /// so nobody else can be stored into that slot while the sample is out.
    /// </summary>
    public class CheckOutSampleDto
    {
        [Required, MaxLength(50)]
        public string SampleId { get; set; } = null!;

        [Required, MaxLength(300)]
        public string Reason { get; set; } = null!;   // test / experiment name

        public DateTime? ExpectedReturnAt { get; set; }

        [MaxLength(100)]
        public string? PerformedBy { get; set; }
    }

    /// <summary>
    /// Return a checked-out sample. Two outcomes:
    ///  - Consumed = false → sample goes back to Stored in its ORIGINAL tube/position,
    ///    RemainingVolumeMl updated.
    ///  - Consumed = true (or RemainingVolumeMl &lt;= 0) → sample is Disposed and the
    ///    tube position is finally freed (TubeStatus.Available).
    /// </summary>
    public class CheckInSampleDto
    {
        [Required, MaxLength(50)]
        public string SampleId { get; set; } = null!;

        public decimal? RemainingVolumeMl { get; set; }

        public bool Consumed { get; set; } = false;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? PerformedBy { get; set; }
    }



    public class UpdateSampleDto
    {
        [Required, MaxLength(50)]
        public string SampleId { get; set; } = null!;

        [MaxLength(200)] public string? Name { get; set; }
        [MaxLength(100)] public string? ExternalId { get; set; }
        [MaxLength(100)] public string? SampleType { get; set; }
        [MaxLength(100)] public string? PatientRef { get; set; }
        [MaxLength(50)] public string? ProjectCode { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        [MaxLength(500)] public string? Notes { get; set; }

        public BloodType? BloodType { get; set; }
        [MaxLength(150)] public string? FastaAccessCode { get; set; }
        public decimal? VolumeMl { get; set; }
        [MaxLength(100)] public string? CollectedByEmployeeId { get; set; }

        [MaxLength(100)] public string? PerformedBy { get; set; }
    }

    /// <summary>Full ancestor chain of a box — lets the frontend rebuild currentPath
    /// and navigate straight to a box from a search result, with a correct breadcrumb.</summary>
    public class BoxLocationDto
    {
        public string FreezerId { get; set; } = null!;
        public int? ShelfIndex { get; set; }        // null for -150°C freezers (no shelves)
        public string? RackId { get; set; }
        public int RackIndex { get; set; }
        public string? DrawerId { get; set; }       // null for -150°C freezers (no drawers)
        public int? DrawerIndex { get; set; }
        public string BoxId { get; set; } = null!;
    }

    /// <summary>Result of "find me the next empty tube" — either freezer-scoped or global.</summary>
    public class NextAvailableSlotDto
    {
        public string FreezerId { get; set; } = null!;
        public string BoxId { get; set; } = null!;
        public string Position { get; set; } = null!;
    }
}