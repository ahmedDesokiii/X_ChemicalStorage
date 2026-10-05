using X_ChemicalStorage.BioBank.DTOs;

namespace X_ChemicalStorage.BioBank.Services
{
    public interface IBackupService
    {
        Task<BackupResultDto> RunAsync(string type, string triggeredBy, CancellationToken ct = default);
        Task<BackupStatusDto> GetStatusAsync();
    }
}
