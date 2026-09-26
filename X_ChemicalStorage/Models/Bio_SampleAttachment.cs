namespace X_ChemicalStorage.Models
{
    public class Bio_SampleAttachment
    {
        public string Id { get; set; } = null!;           // GUID
        public string SampleId { get; set; } = null!;      // FK → Bio_Sample
        public AttachmentCategory Category { get; set; }   // Fasta = 1, General = 2
        public string OriginalFileName { get; set; } = null!;
        public string StoredFileName { get; set; } = null!; // الاسم الفعلي على الديسك (GUID-based, يمنع تعارض الأسماء)
        public string RelativePath { get; set; } = null!;   // للـ download endpoint
        public long FileSizeBytes { get; set; }
        public string ContentType { get; set; } = null!;    // MIME type
        public DateTime UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }
    public enum AttachmentCategory
    {
        Fasta = 1,
        General = 2
    }
}
