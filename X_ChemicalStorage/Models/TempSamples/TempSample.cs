using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models.TempSamples
{
    /// <summary>
    /// A sample received for TEMPORARY custody (e.g. held for a lab request, then withdrawn).
    /// Completely separate table from Lot — shares only the physical Room→Unit→Shelf→Box hierarchy.
    /// </summary>
    [Table("TS_Samples")]
    public class TempSample
    {
        public int Id { get; set; }

        // ───────── Identity ─────────
        /// <summary>Human-readable, sequential: TS-2026-000123</summary>
        [Required, MaxLength(30)]
        public string SampleCode { get; set; } = default!;

        /// <summary>Relative URL of the generated QR image (wwwroot/tempsample-qr/...)</summary>
        [MaxLength(200)]
        public string? QrImagePath { get; set; }

        /// <summary>Optional external barcode already printed on the sample by the sender.</summary>
        [MaxLength(100)]
        public string? ExternalBarcode { get; set; }

        // ───────── Hand-over (who / when) ─────────
        public DateTime DeliveryDateTime { get; set; }
        public DateTime ReceptionDateTime { get; set; }

        [Required, MaxLength(150)]
        public string DeliveredByName { get; set; } = default!;

        [MaxLength(200)]
        public string? DeliveringOrganization { get; set; }

        public int ReceivedByUserId { get; set; }
        [Required, MaxLength(150)]
        public string ReceivedByName { get; set; } = default!;

        [MaxLength(200)]
        public string? ReceivingOrganization { get; set; }

        // ───────── What ─────────
        [Required, MaxLength(100)]
        public string SampleType { get; set; } = default!;   // Blood, Serum, Swab, Tissue, Chemical...

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal QuantityReceived { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal CurrentQuantity { get; set; }

        public int? UnitId { get; set; }          // reuses the existing Units table
        public Unit? Unit { get; set; }

        public int ContainersCount { get; set; } = 1;

        // ───────── Why ─────────
        [MaxLength(500)]
        public string? RequestedAnalysis { get; set; }

        [MaxLength(100)]
        public string? RequestReferenceNumber { get; set; }

        // ───────── Transport ─────────
        public TransportCondition TransportCondition { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? ArrivalTemperatureC { get; set; }

        /// <summary>Calculated at reception: arrival temp outside the declared transport range.</summary>
        public bool ArrivedOutOfRange { get; set; }

        [MaxLength(2000)]
        public string? Comments { get; set; }

        // ───────── Retention ─────────
        /// <summary>Date the sample is expected to be withdrawn (drives "overdue" alerts in phase 3).</summary>
        public DateTime? PlannedWithdrawalDate { get; set; }

        // ───────── Status & Location ─────────
        public TempSampleStatus Status { get; set; } = TempSampleStatus.Received;

        /// <summary>Null when not occupying a slot (Received / Withdrawn / Disposed).</summary>
        public int? BoxId { get; set; }
        public Box? Box { get; set; }
        public int? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }

        // ───────── Audit ─────────
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<TempSampleCustodyEvent> CustodyEvents { get; set; } = new List<TempSampleCustodyEvent>();
        public ICollection<TempSampleAttachment> Attachments { get; set; } = new List<TempSampleAttachment>();
    }
}
