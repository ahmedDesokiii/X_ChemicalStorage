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
            
            model.SdsStatus = new SdsStatusChartVm
            {
                ValidCount = items.Count(x => x.SDS==true),
                InvalidCount = items.Count(x =>x.SDS == false)
            };
            var storageSummary = new StorageConditionSummaryVm
            {
                RoomTempCount = items.Count(x => x.StorageCondition.Trim().ToLower() == "room temp"),
                FreezerCount = items.Count(x => x.StorageCondition.Trim().ToLower().Contains("freezer")),
                ColdCount = items.Count(x => x.StorageCondition.Trim().Contains("2"))
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
