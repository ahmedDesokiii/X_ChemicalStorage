// Services/ISampleService.cs
using X_ChemicalStorage.BioBank.DTOs;

namespace X_ChemicalStorage.BioBank.Services
{
    public interface ISampleService
    {
        Task<Bio_SampleDto> StoreAsync(StoreSampleDto dto);
        Task<Bio_SampleDto> ReserveAsync(ReserveSampleDto dto);
        Task<bool> CancelReserveAsync(string boxId, string position, string? performedBy = null);
        Task<Bio_SampleDto> WithdrawAsync(WithdrawSampleDto dto);
        Task<Bio_SampleDto> TransferAsync(TransferSampleDto dto);
        Task<Bio_SampleDto?> GetByIdAsync(string sampleId);
        Task<List<Bio_SampleMovementDto>> GetHistoryAsync(string sampleId);
        Task<List<Bio_SampleDto>> SearchAsync(string query);
        Task<TubeSampleDetailsDto?> GetTubeDetailsAsync(string boxId, string position);
    }
}