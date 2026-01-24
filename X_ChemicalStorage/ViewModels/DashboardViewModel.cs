
namespace X_ChemicalStorage.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalItems { get; set; }
        public double? TotalAvailableQuantity { get; set; }
        public int LowStockItems { get; set; }
        public int ExpiredLots { get; set; }
    }
}
