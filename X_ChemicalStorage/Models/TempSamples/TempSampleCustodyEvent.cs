using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models.TempSamples
{
    /// <summary>
    /// Append-only chain-of-custody log. Rows are NEVER updated or deleted.
    /// Each row stores the SHA-256 of the previous row → any tampering breaks the chain
    /// and is detected by VerifyChainAsync (ALCOA+ / ISO 20387 friendly).
    /// </summary>
    [Table("TS_CustodyEvents")]
    public class TempSampleCustodyEvent
    {
        public long Id { get; set; }

        public int TempSampleId { get; set; }
        public TempSample TempSample { get; set; } = default!;

        /// <summary>1, 2, 3 … per sample (unique together with TempSampleId).</summary>
        public int Sequence { get; set; }

        public CustodyEventType EventType { get; set; }
        public DateTime OccurredAtUtc { get; set; }

        public int? PerformedByUserId { get; set; }
        [Required, MaxLength(150)]
        public string PerformedByName { get; set; } = default!;

        /// <summary>The other party: who handed it over / who received it on withdrawal.</summary>
        [MaxLength(150)]
        public string? Counterparty { get; set; }

        /// <summary>Location snapshots as text — survive even if the box is later renamed/deleted.</summary>
        [MaxLength(300)] public string? FromLocation { get; set; }
        [MaxLength(300)] public string? ToLocation { get; set; }

        [Column(TypeName = "decimal(18,3)")] public decimal? QuantityDelta { get; set; }
        [Column(TypeName = "decimal(18,3)")] public decimal? QuantityAfter { get; set; }

        public TempSampleStatus? StatusAfter { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Optional JSON payload (e.g. field-level diff on MetadataUpdated).</summary>
        public string? DataJson { get; set; }

        [MaxLength(100)]
        public string? ClientInfo { get; set; }   // device / IP

        [Required, MaxLength(64)]
        public string PreviousHash { get; set; } = default!;

        [Required, MaxLength(64)]
        public string Hash { get; set; } = default!;
    }
}
