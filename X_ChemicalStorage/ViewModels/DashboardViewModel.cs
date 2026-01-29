
using X_ChemicalStorage.Dtos;

namespace X_ChemicalStorage.ViewModels
{
    public class DashboardViewModel
    {
        //// KPIs
        public int TotalItems { get; set; }
        public int TotalLots { get; set; }

        public double? TotalAvailableQty { get; set; }
        public int UnderLimitItems { get; set; }
        public int ExpiringLots { get; set; }
        public int NearExpiringLots { get; set; }
        public int HazardItems { get; set; }

        // Alerts Items
        public bool HasUnderLimitAlert => UnderLimitItems > 0;
        public bool HasExpiryAlert => ExpiringLots > 0;
        public bool HasNearExpiryAlert => NearExpiringLots > 0;
        public bool HasHazardAlert => HazardItems > 0;

        // Charts
        public List<string> ItemNames { get; set; } = new();
        public List<double?> ItemQuantities { get; set; } = new();

       /* ===== DONUT DATA ===== */
        public SdsStatusChartVm SdsStatus { get; set; } = new();
        public StorageConditionSummaryVm StorageCondition { get; set; }
    }
    // Item VM
    public class SdsStatusChartVm
    {
        public int ValidCount { get; set; }
        public int InvalidCount { get; set; }
    }
    public class StorageConditionSummaryVm
    {
        public int RoomTempCount { get; set; }
        public int FreezerCount { get; set; }
        public int ColdCount { get; set; }

        public int Total =>
            RoomTempCount + FreezerCount + ColdCount;

        public int RoomTempPercent =>
            Total == 0 ? 0 : (RoomTempCount * 100 / Total);

        public int FreezerPercent =>
            Total == 0 ? 0 : (FreezerCount * 100 / Total);

        public int ColdPercent =>
            Total == 0 ? 0 : (ColdCount * 100 / Total);
    }
}
