using Microsoft.EntityFrameworkCore;


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
            return View(model);
        }
        //public IActionResult MasterDashboard()
        //{
            
        //    var today = DateTime.Today;
        //    var next30Days = today.AddDays(30);

        //    // 1️⃣ KPI CARDS
        //    var totalItems =  _context.Items.CountAsync();

        //    var lowStockItems =  _context.Items
        //    .Where(i => i.AvilableQuantity <= i.Limit)
        //    .CountAsync();

        //    var expiredLots =  _context.Lots
        //                    .Where(l => l.ExpiryDate < today)
        //                    .CountAsync();


        //    var expiringSoonLots =  _context.Lots
        //        .Where(l => l.ExpiryDate >= today && l.ExpiryDate <= next30Days)
        //        .CountAsync();

        //    // 2️⃣ Stock IN vs OUT (Monthly)
        //    var transactions =  _context.ItemTransactions
        //        .Where(t => t.Move_Date.Value.Year == today.Year)
        //        .GroupBy(t => new { t.Move_Date.Value.Month, t.Move_State })
        //        .Select(g => new
        //        {
        //            Month = g.Key.Month,
        //            Type = g.Key.Move_State,
        //            Quantity = g.Sum(x => x.Move_Quantity)
        //        })
        //        .ToListAsync();

        //    var months = Enumerable.Range(1, 12)
        //    .Select(m => new DateTime(today.Year, m, 1).ToString("MMM"))
        //    .ToList();

        //    var stockIn = new List<decimal>();
        //    var stockOut = new List<decimal>();

        //    for (int m = 1; m <= 12; m++)
        //    {
        //        stockIn.Add((decimal)transactions.Result
        //            .Where(t => t.Month == m && t.Type == true)
        //            .Sum(t => t.Quantity)
        //        );

        //        stockOut.Add(
        //            (decimal)transactions.Result
        //            .Where(t => t.Month == m && t.Type == false)
        //            .Sum(t => t.Quantity)
        //        );
        //    }

        //    // 3️⃣ Top Used Chemicals
        //    var topItems =  _context.ItemTransactions
        //        .Where(t => t.Move_State == false)
        //        .GroupBy(t => t.ItemId)
        //        .Select(g => new
        //        {
        //            ItemName = g.First().Item.Name,
        //            Quantity = g.Sum(x => x.Move_Quantity)
        //        })
        //        .OrderByDescending(x => x.Quantity)
        //        .Take(5)
        //        .ToListAsync();


        //    // 4️⃣ Expired Lots Table
        //    var expiredLotsList =  _context.Lots
        //        .Include(l => l.Item)
        //        .Include(l => l.Location)
        //        .Where(l => l.ExpiryDate < today)
        //        .Select(l => new Lot
        //        {
        //            //Item.Name = l.Item.Name,
        //            LotNumber = l.LotNumber,
        //            ExpiryDate = l.ExpiryDate,
        //            AvilableQuantity = l.AvilableQuantity,
        //            //LocationName = l.Location.Name
        //        })
        //        .ToListAsync();

        //    // 5️⃣ ViewModel
        //     model = new DashboardViewModel
        //    {
        //        //TotalItems = Convert.ToInt32(totalItems),
        //        //UnderLimitItems = Convert.ToInt32(lowStockItems),
        //        //ExpiringLots = Convert.ToInt32(expiredLots),
        //        //ExpiringSoonLots = Convert.ToInt32(expiringSoonLots),

        //        //Months = months,
        //        //StockIn = stockIn,
        //        //StockOut = stockOut,

        //        //TopItemsNames = topItems.Result.Select(x => x.ItemName).ToList(),
        //        //TopItemsQuantities = topItems.Result.Select(x => x.Quantity).ToList(),

        //        //ExpiredLotsList = expiredLotsList.Result
        //    };

        //    return View(model);

        //}

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
