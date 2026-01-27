using Microsoft.EntityFrameworkCore;

namespace X_ChemicalStorage.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }
        public DashboardViewModel GetDashboard()
        {
            var totalItems = _context.Items.Count();
            var totalLots = _context.Lots.Count();

            var totalAvailableQty = _context.Items
                .Sum(i => (float?)i.AvilableQuantity) ?? 0;

            // 🔴 Under Limit
            var underLimitItems = _context.Items
                .Count(i => i.AvilableQuantity <= i.Limit);

            // ⏳ Expiry (30 days)
            var expiringLots = _context.Lots
                .Count(l => l.ExpiryDate <= DateTime.Today.AddDays(30)
                         && l.AvilableQuantity > 0);

            // ☣️ Hazard (SDS)
            var hazardItems = _context.Items
                .Count(i => i.SDS==true);

            // Chart (Top Items)
            var chartData = _context.Items
                .OrderByDescending(i => i.AvilableQuantity)
                .Take(10)
                .Select(i => new
                {
                    i.Name,
                    Qty = i.AvilableQuantity
                })
                .ToList();

            return new DashboardViewModel
            {
                TotalItems = totalItems,
                TotalLots = totalLots,
                TotalAvailableQty = totalAvailableQty,
                UnderLimitItems = underLimitItems,
                ExpiringLots = expiringLots,
                HazardItems = hazardItems,

                ItemNames = chartData.Select(x => x.Name).ToList(),
                ItemQuantities = chartData.Select(x => x.Qty).ToList()
            };
        }
    }
}



