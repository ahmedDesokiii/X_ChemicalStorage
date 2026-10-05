using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace X_ChemicalStorage.Models
{
    /// سجل النسخ الاحتياطي (تلقائي ويدوي).
    [Index(nameof(StartedAt))]
    public class Bio_BackupLog
    {
        [Key]
        public long Id { get; set; }

        public DateTime StartedAt { get; set; }               // UTC
        public DateTime? FinishedAt { get; set; }             // UTC

        [Required, MaxLength(10)]
        public string Type { get; set; } = "Manual";          // Auto | Manual

        [Required, MaxLength(10)]
        public string Status { get; set; } = "Failed";        // Success | Failed

        [MaxLength(500)]
        public string? FilePath { get; set; }

        [MaxLength(200)]
        public string? FileName { get; set; }

        public long? SizeBytes { get; set; }
        public int DurationMs { get; set; }
        public bool Verified { get; set; }                    // RESTORE VERIFYONLY نجح
        public bool CopiedToSecondary { get; set; }

        [MaxLength(200)]
        public string? AttachmentsFileName { get; set; }      // ZIP المرفقات (لو اتعمل)

        [MaxLength(1000)]
        public string? Error { get; set; }

        [MaxLength(100)]
        public string? TriggeredBy { get; set; }
    }
}