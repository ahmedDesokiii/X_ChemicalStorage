// Services/IBioAttachmentService.cs
using Microsoft.AspNetCore.Http;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.BioBank.Services
{
    public interface IBioAttachmentService
    {
        Task<List<Bio_SampleAttachmentDto>> GetAttachmentsAsync(string sampleId, AttachmentCategory? category = null);
        Task<Bio_SampleAttachmentDto> UploadAsync(string sampleId, AttachmentCategory category, IFormFile file, string? performedBy);
        Task<AttachmentFileResult?> DownloadAsync(string attachmentId);
        Task<bool> DeleteAsync(string attachmentId);
    }

    public class AttachmentFileResult
    {
        public Stream Stream { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string FileName { get; set; } = null!;
    }
}