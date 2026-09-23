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

        // ===== NEW: check-out / check-in workflow =====

        /// <summary>Take a stored sample out for testing. Locks its tube slot (TubeStatus.CheckedOut).</summary>
        Task<Bio_SampleDto> CheckOutAsync(CheckOutSampleDto dto);

        /// <summary>Return a checked-out sample: back to Stored (same tube), or Disposed if consumed.</summary>
        Task<Bio_SampleDto> CheckInAsync(CheckInSampleDto dto);

        /// <summary>All samples currently checked out, ordered by soonest expected return first (nulls last).</summary>
        Task<List<Bio_SampleDto>> GetActiveCheckOutsAsync();

        /// <summary>Checked-out samples whose ExpectedReturnAt has passed — for the "overdue" alert view.</summary>
        Task<List<Bio_SampleDto>> GetOverdueCheckOutsAsync();
        // Add inside the interface, e.g. right after GetOverdueCheckOutsAsync():
        Task<string> GenerateNextSampleIdAsync();

        // Add inside the interface:
        Task<Bio_SampleDto> UpdateAsync(UpdateSampleDto dto);
        Task<BoxLocationDto?> GetBoxLocationAsync(string boxId);
        Task<NextAvailableSlotDto?> FindNextAvailableSlotAsync(string? freezerId = null);
    }
}