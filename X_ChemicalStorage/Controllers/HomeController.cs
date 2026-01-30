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
                .Include(i => i.Category)
                .AsNoTracking()
                .ToList();

            /* ===== STOCK DONUT (LOTS – AVAILABLE QTY) ===== */
            // Item SDS 
            model.SdsWaveItems = items.Select(i => new SdsWaveItemVm
            {
                ItemName = i.Name,
                Quantity = (int)i.AvilableQuantity,
                IsValid = i.SDS == true
            })  .ToList();
            //lot charts
            model.LotsOverTime = lots
                .GroupBy(x => x.ReceivedDate.Value.Month)
                .Select(g => new TimeSeriesPointVm
                {
                    Label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key),
                    Value = g.Count()
                })
                .OrderBy(x => x.Label)
                .ToList();

            model.ExpiryTrend = lots
                .Where(x => x.ExpiryDate <= DateTime.Today.AddMonths(6))
                .GroupBy(x => x.ExpiryDate.Value.Month)
                .Select(g => new TimeSeriesPointVm
                {
                    Label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key),
                    Value = g.Count()
                })
                .OrderBy(x => x.Label)
                .ToList();

            // Storage Condition
            var storageSummary = new StorageConditionSummaryVm
            {
                RoomTempCount = items.Count(x => x.StorageCondition.Trim().ToLower() == "room temp"),
                FreezerCount = items.Count(x => x.StorageCondition.Trim().ToLower().Contains("freezer")),
                ColdCount = items.Count(x => x.StorageCondition.Trim().Contains("2-8"))
            };
            model.StorageCondition = storageSummary;

            //Items By Category
            model.CategorySummary = items
            .GroupBy(x => x.Category.Name)
            .Select(g => new CategorySummaryVm
            {
                CategoryName = g.Key,
                ItemsCount = g.Count()
            })
            .ToList();

            return View(model);
        }

       

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
