
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
        public int HazardItems { get; set; }

        // Alerts
        public bool HasUnderLimitAlert => UnderLimitItems > 0;
        public bool HasExpiryAlert => ExpiringLots > 0;
        public bool HasHazardAlert => HazardItems > 0;

        // Charts
        public List<string> ItemNames { get; set; } = new();
        public List<double?> ItemQuantities { get; set; } = new();

        public List<LotTimelineDto> LotTimeline { get; set; } = new();
        public List<TopRiskLotDto> TopRiskLots { get; set; } = new();
        public List<ItemTimelineDto> ItemTimeline { get; set; }
    }
}
