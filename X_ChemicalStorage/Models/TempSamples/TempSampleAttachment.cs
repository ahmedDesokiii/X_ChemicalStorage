using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models.TempSamples
{
    /// <summary>Delivery documents (delivery note, request form, temp log, photos…).
    /// Stored outside wwwroot (App_Data/tempsample-uploads) — same pattern as BioAttachmentService.</summary>
    [Table("TS_Attachments")]
    public class TempSampleAttachment
    {
        public int Id { get; set; }

        public int TempSampleId { get; set; }
        public TempSample TempSample { get; set; } = default!;

        public AttachmentKind Kind { get; set; }

        [Required, MaxLength(255)] public string OriginalFileName { get; set; } = default!;
        [Required, MaxLength(300)] public string RelativePath { get; set; } = default!;
        [Required, MaxLength(100)] public string ContentType { get; set; } = default!;
        public long SizeBytes { get; set; }

        /// <summary>SHA-256 of the file content — proves the document wasn't swapped later.</summary>
        [Required, MaxLength(64)] public string Sha256 { get; set; } = default!;

        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
        [MaxLength(150)] public string? UploadedBy { get; set; }
    }
}
