using X_ChemicalStorage.BioBank.DTOs;

namespace X_ChemicalStorage.BioBank.Services
{
    public interface IFreezerIoTService
    {
        Task<IngestResult> ProcessIncomingReadingAsync(IngestReadingDto dto);
        Task<Bio_IoTDeviceDto> LinkDeviceAsync(LinkIoTDeviceDto dto);
        Task<List<Bio_FreezerReadingDto>> GetHistoricalAsync(string freezerId, DateTime from, DateTime to);
        Task<List<Bio_FreezerAlarmDto>> GetActiveAlarmsAsync(string? freezerId = null);
        Task AcknowledgeAlarmAsync(long alarmId, string performedBy, string? note = null);
        Task<Bio_IoTDeviceDto?> GetDeviceByFreezerAsync(string freezerId);
    }
}