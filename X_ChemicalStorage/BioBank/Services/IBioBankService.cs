// Services/IBioBankService.cs
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.BioBank.Services
{
    public interface IBioBankService
    {
        // ===== Freezers =====
        Task<List<Bio_FreezerDto>> GetAllFreezersAsync();
        Task<Bio_FreezerDto?> GetFreezerByIdAsync(string id);
        Task<Bio_FreezerDto> CreateFreezerAsync(Bio_CreateFreezerDto dto);
        Task<bool> DeleteFreezerAsync(string id);

        // ===== Hierarchy Navigation =====
        Task<List<Bio_ShelfDto>> GetShelvesByFreezerAsync(string freezerId);
        Task<List<Bio_RackDto>> GetRacksByShelfAsync(string freezerId, int shelfIndex);
        Task<List<Bio_RackDto>> GetRacksByFreezerAsync(string freezerId);          // for -150
        Task<List<Bio_DrawerDto>> GetDrawersByRackAsync(string rackId);
        Task<List<Bio_BoxDto>> GetBoxesByDrawerAsync(string drawerId);
        Task<List<Bio_BoxDto>> GetBoxesByRackAsync(string rackId);                 // for -150
        Task<Bio_BoxDetailsDto?> GetBoxDetailsAsync(string boxId);
        Task<Bio_FreezerDto> RebuildMinus150BoxesAsync(string freezerId, int? boxesPerRack = null);


        // ===== Search =====
        Task<List<Bio_FreezerDto>> SearchFreezersAsync(string query);

        // ===== Occupancy Helpers =====
        Task<int> CalculateFreezerOccupancyAsync(string freezerId);



        // ===== Tube / Box Updates =====
        Task<Bio_TubeDto?> UpdateTubeAsync(string boxId, string position, Bio_UpdateTubeDto dto);
        Task<Bio_TubeDto> OccupyTubeAsync(string boxId, Bio_OccupyTubeDto dto);
        Task<List<Bio_TubeDto>> BulkOccupyTubesAsync(string boxId, Bio_BulkOccupyTubesDto dto);
        Task<Bio_TubeDto> ReserveTubeAsync(string boxId, Bio_ReserveTubeDto dto);
        Task<bool> ClearTubeAsync(string boxId, string position);
    }
}