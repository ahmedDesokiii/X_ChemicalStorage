using System.ComponentModel.DataAnnotations;

namespace X_ChemicalStorage.BioBank.DTOs
{
    public static class IoTState
    {
        public const string Normal = "Normal";
        public const string HighTemp = "HighTemp";
        public const string LowTemp = "LowTemp";
        public const string Abnormal = "Abnormal";          // باب / كهرباء / حساس
        public const string Offline = "Offline";
        public const string Commissioning = "Commissioning";
    }

    public class IoTCountsDto
    {
        public int All { get; set; }
        public int Normal { get; set; }
        public int HighTemp { get; set; }
        public int LowTemp { get; set; }
        public int Abnormal { get; set; }
        public int Offline { get; set; }
        public int Commissioning { get; set; }
    }

    public class IoTTrendPointDto
    {
        public string Date { get; set; } = "";     // yyyy-MM-dd بتوقيت الإعداد المحلي
        public int HighTemp { get; set; }
        public int LowTemp { get; set; }
        public int Other { get; set; }
    }

    public class IoTTopDeviceDto
    {
        public string FreezerId { get; set; } = "";
        public int HighTemp { get; set; }
        public int LowTemp { get; set; }
        public int Other { get; set; }
        public int Total { get; set; }
    }

    public class IoTDeviceStateDto
    {
        public string FreezerId { get; set; } = "";
        public string Type { get; set; } = "";
        public string? CollectorCode { get; set; }          // أحدث جهاز نشط على الفريزر
        public string State { get; set; } = IoTState.Normal;
        public decimal? Temperature { get; set; }
        public DateTime? LastReadingAt { get; set; }
        public decimal HighTempLimit { get; set; }
        public decimal LowTempLimit { get; set; }
        public int UnacknowledgedAlarms { get; set; }
        public List<string> ActiveAlarmTypes { get; set; } = new();
    }

    public class IoTDashboardDto
    {
        public int Days { get; set; }
        public IoTCountsDto Counts { get; set; } = new();
        public List<IoTTrendPointDto> Trend { get; set; } = new();
        public List<IoTTopDeviceDto> TopDevices { get; set; } = new();
        public List<IoTDeviceStateDto> Devices { get; set; } = new();
    }

    public class FreezerSettingsDto
    {
        public string FreezerId { get; set; } = "";
        public decimal HighTempLimit { get; set; }
        public decimal LowTempLimit { get; set; }
        public int TempAlarmDelayMinutes { get; set; }
        public int DoorOpenMinutes { get; set; }
        public int OfflineAfterMinutes { get; set; }
        public int EscalationMinutes { get; set; }
        public bool IsCommissioning { get; set; }
        public DateTime? CommissioningEndedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class UpdateFreezerSettingsDto
    {
        [Required] public decimal? HighTempLimit { get; set; }
        [Required] public decimal? LowTempLimit { get; set; }
        [Required] public int? TempAlarmDelayMinutes { get; set; }
        [Required] public int? DoorOpenMinutes { get; set; }
        [Required] public int? OfflineAfterMinutes { get; set; }
        [Required] public int? EscalationMinutes { get; set; }
        [Required] public bool? IsCommissioning { get; set; }
    }

    public class SettingsAuditDto
    {
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = "";
        public string Field { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }
}