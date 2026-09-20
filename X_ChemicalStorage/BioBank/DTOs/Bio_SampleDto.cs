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
}