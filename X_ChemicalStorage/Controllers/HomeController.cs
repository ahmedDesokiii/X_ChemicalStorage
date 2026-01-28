using Microsoft.EntityFrameworkCore;
using System.Linq;
using static X_ChemicalStorage.Constants.Permissions;


namespace X_ChemicalStorage.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IServicesRepository<Item> _servicesItem;
        private readonly IServicesRepository<Lot> _servicesLot;
        private readonly IServicesItem _servicesOfItem;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IDashboardService _service;

        
        public HomeController(IWebHostEnvironment env, IServicesRepository<Item> servicesItem, IServicesRepository<Lot> servicesLot, IServicesItem servicesOfItem, UserManager<ApplicationUser> userManager, ApplicationDbContext context, IDashboardService service)
        {
            _env = env;
            _servicesItem = servicesItem;
            _servicesLot = servicesLot;
            _servicesOfItem = servicesOfItem;
            _userManager = userManager;
            _context = context;
            _service = service;
        }

        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult MasterDashboard()
        {
            var model = _service.GetDashboard();

            const int EXPIRING_DAYS = 30;
            var today = DateTime.Today;
            var lots = _context.Lots
                .Include(l => l.Item)
                .AsNoTracking()
                .ToList();

            var items = _context.Items
                .Include(i => i.Lots)
                .AsNoTracking()
                .ToList();

            /* ===== STOCK DONUT (LOTS – AVAILABLE QTY) ===== */
            model.StockDonut = new StockDonutDto
            {
                Available = lots
                    .Where(l => l.AvilableQuantity > 0 && l.ExpiryDate < today)
                    .Sum(l => l.AvilableQuantity),

                Low = lots
                    .Where(l =>
                        l.AvilableQuantity > 0 &&
                        l.AvilableQuantity <= l.Item.Limit &&
                        l.ExpiryDate < today)
                    .Sum(l => l.AvilableQuantity),

                Expired = lots
                    .Where(l => l.ExpiryDate < today)
                    .Sum(l => l.AvilableQuantity)
            };

            /* ===== ITEM DONUT (ITEM COUNT) ===== */
            model.ItemStock = new ItemStockDonutDto
            {
                Available = items
                    .Count(i => i.AvilableQuantity > 0 &&
                           i.Lots.Any(l => l.ExpiryDate > today.AddDays(-1* EXPIRING_DAYS))),

                Low = items
                    .Count(i =>
                        i.AvilableQuantity > 0 &&
                        //i.AvilableQuantity <= i.Limit &&
                        i.Lots.Any(l => l.ExpiryDate >= today && l.ExpiryDate <= today.AddDays(EXPIRING_DAYS))),

                Expiring = items
                    .Count(i => i.Lots.Any(l => l.ExpiryDate <= today))
            };

            model.LotTimeline = lots
            .Where(l => l.ExpiryDate != null)
            .Select(l => new LotTimelineDto
            {
                x = l.LotNumber,   //  حسب اسم العمود
                y = new List<DateTime>
                {
                    Convert.ToDateTime(l.ManufactureDate),   // تاريخ التصنيع
                    Convert.ToDateTime(l.ExpiryDate)         // تاريخ الانتهاء
                }
            })
            .ToList();

            
            model.SdsStatus = new SdsStatusChartVm
            {
                ValidCount = items.Count(x => x.SDS==true),
                InvalidCount = items.Count(x =>x.SDS == false)
            };
            var storageSummary = new StorageConditionSummaryVm
            {
                RoomTempCount = items.Count(x => x.StorageCondition == "RoomTemp"),
                FreezerCount = items.Count(x => x.StorageCondition == "Freezer20"),
                ColdCount = items.Count(x => x.StorageCondition == "2-8")
            };

            model.StorageCondition = storageSummary;

            return View(model);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
