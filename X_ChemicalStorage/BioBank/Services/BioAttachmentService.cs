// Services/BioAttachmentService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    public class BioAttachmentService : IBioAttachmentService
    {
        private readonly ApplicationDbContext _db;
        private readonly string _storageRoot;

        private static readonly HashSet<string> DeniedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".sh", ".msi", ".com", ".scr",
            ".ps1", ".vbs", ".vbe", ".js", ".jse", ".jar", ".app", ".cpl",
            ".gadget", ".paf", ".lnk", ".reg", ".wsf", ".wsh", ".msc"
        };

        private const long FastaMaxBytes = 10L * 1024 * 1024;     // 10 MB
        private const long GeneralMaxBytes = 100L * 1024 * 1024;  // 100 MB

        public BioAttachmentService(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            // Outside wwwroot on purpose — never directly URL-reachable, always through the download endpoint.
            _storageRoot = Path.Combine(env.ContentRootPath, "App_Data", "biobank-uploads");
            Directory.CreateDirectory(_storageRoot);
        }

        public async Task<List<Bio_SampleAttachmentDto>> GetAttachmentsAsync(string sampleId, AttachmentCategory? category = null)
        {
            var query = _db.Bio_SampleAttachments.AsNoTracking().Where(a => a.SampleId == sampleId);
            if (category.HasValue) query = query.Where(a => a.Category == category.Value);

            var list = await query.OrderByDescending(a => a.UploadedAt).ToListAsync();
            return list.Select(MapDto).ToList();
        }

        public async Task<Bio_SampleAttachmentDto> UploadAsync(string sampleId, AttachmentCategory category, IFormFile file, string? performedBy)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided.");

            var sample = await _db.Bio_Samples.FirstOrDefaultAsync(s => s.Id == sampleId)
                ?? throw new InvalidOperationException($"Sample '{sampleId}' not found.");

            var ext = Path.GetExtension(file.FileName);

            if (category == AttachmentCategory.Fasta)
            {
                if (!string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("FASTA Access Code must be a .txt file.");
                if (file.Length > FastaMaxBytes)
                    throw new ArgumentException("FASTA file exceeds the 10 MB limit.");
            }
            else
            {
                if (DeniedExtensions.Contains(ext))
                    throw new ArgumentException($"File type '{ext}' is not allowed.");
                if (file.Length > GeneralMaxBytes)
                    throw new ArgumentException("File exceeds the 100 MB limit.");
            }

            return await ExecuteInTransactionAsync(async () =>
            {
                if (category == AttachmentCategory.Fasta)
                {
                    var previous = await _db.Bio_SampleAttachments
                        .Where(a => a.SampleId == sampleId && a.Category == AttachmentCategory.Fasta)
                        .ToListAsync();

                    foreach (var old in previous)
                    {
                        DeleteFileQuietly(old.RelativePath);
                        _db.Bio_SampleAttachments.Remove(old);
                    }
                }

                var sampleFolder = Path.Combine(_storageRoot, sampleId);
                Directory.CreateDirectory(sampleFolder);

                var storedFileName = $"{Guid.NewGuid():N}{ext}";
                var relativePath = Path.Combine(sampleId, storedFileName).Replace('\\', '/');
                var fullPath = Path.Combine(_storageRoot, relativePath);

                await using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var attachment = new Bio_SampleAttachment
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SampleId = sampleId,
                    Category = category,
                    OriginalFileName = file.FileName,
                    StoredFileName = storedFileName,
                    RelativePath = relativePath,
                    FileSizeBytes = file.Length,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    UploadedAt = DateTime.UtcNow,
                    UploadedBy = performedBy
                };
                _db.Bio_SampleAttachments.Add(attachment);

                // Store just a short, safe reference (the file name) — never the raw file
                // content, which can be far larger than the column allows and would blow up
                // the save with a SQL truncation error. Full content is always available via
                // the attachment download endpoint.
                if (category == AttachmentCategory.Fasta)
                {
                    const int maxLen = 150;
                    sample.FastaAccessCode = attachment.OriginalFileName.Length > maxLen
                        ? attachment.OriginalFileName.Substring(0, maxLen)
                        : attachment.OriginalFileName;
                    sample.UpdatedAt = DateTime.UtcNow;
                }

                return MapDto(attachment);

            });
        }

        public async Task<AttachmentFileResult?> DownloadAsync(string attachmentId)
        {
            var attachment = await _db.Bio_SampleAttachments.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null) return null;

            var fullPath = Path.Combine(_storageRoot, attachment.RelativePath);
            if (!File.Exists(fullPath)) return null;

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return new AttachmentFileResult
            {
                Stream = stream,
                ContentType = attachment.ContentType,
                FileName = attachment.OriginalFileName
            };
        }

        public async Task<bool> DeleteAsync(string attachmentId)
        {
            var attachment = await _db.Bio_SampleAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null) return false;

            return await ExecuteInTransactionAsync(async () =>
            {
                DeleteFileQuietly(attachment.RelativePath);

                if (attachment.Category == AttachmentCategory.Fasta)
                {
                    var sample = await _db.Bio_Samples.FirstOrDefaultAsync(s => s.Id == attachment.SampleId);
                    if (sample != null)
                    {
                        sample.FastaAccessCode = null;
                        sample.UpdatedAt = DateTime.UtcNow;
                    }
                }

                _db.Bio_SampleAttachments.Remove(attachment);
                return true;
            });
        }

        private void DeleteFileQuietly(string relativePath)
        {
            try
            {
                var path = Path.Combine(_storageRoot, relativePath);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { /* best-effort */ }
        }

        private static Bio_SampleAttachmentDto MapDto(Bio_SampleAttachment a) => new()
        {
            Id = a.Id,
            SampleId = a.SampleId,
            Category = a.Category,
            OriginalFileName = a.OriginalFileName,
            FileSizeBytes = a.FileSizeBytes,
            ContentType = a.ContentType,
            UploadedAt = a.UploadedAt,
            UploadedBy = a.UploadedBy
        };

        private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return result;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}