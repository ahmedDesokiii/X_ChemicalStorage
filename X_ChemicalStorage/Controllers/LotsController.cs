using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using X_ChemicalStorage.IRepository;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.Controllers
{
    public class LotsController : Controller
    {
        private readonly IServicesRepository<Lot> _servicesLot;
        private readonly IServicesLot _servicesOfLot;
        private readonly IServicesItem _servicesOfItem;
        private readonly IServicesRepository<Item> _servicesItem;
        private readonly IInventoryService _inventoryService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;
        public LotsController(IServicesRepository<Lot> servicesLot, IServicesLot servicesOfLot, IServicesItem servicesOfItem, IServicesRepository<Item> servicesItem, IInventoryService inventoryService, UserManager<ApplicationUser> userManager, ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _servicesLot = servicesLot;
            _servicesOfLot = servicesOfLot;
            _servicesOfItem = servicesOfItem;
            _servicesItem = servicesItem;
            _inventoryService = inventoryService;
            _userManager = userManager;
            _context = context;
            _authorizationService = authorizationService;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of Lots
        [Authorize(Permissions.Lots.View_Lots)]
        public async Task<IActionResult> Index()
        {


            var todayUtc = DateTime.UtcNow.Date;
            var Lots = new LotViewModel
            {
                LotsList = _servicesLot.GetAll()
                                        .OrderBy(x => x.ExpiryDate)
                                        .ThenBy(x => x.ManufactureDate)
                                        .ThenBy(x => x.ReceivedDate)
                                        .ToList(),

                ExpiryLotsList = _servicesLot.GetAll()
                                            .Where(x => x.ExpiryDate != null && x.ExpiryDate.Value.Date <= DateTime.Today)
                                            .OrderBy(x => x.ExpiryDate)
                                            .ThenBy(x => x.ManufactureDate)
                                            .ThenBy(x => x.ReceivedDate)
                                            .ToList(),

                ItemsList = _servicesItem.GetAll()
                                            .OrderByDescending(x => x.StorageUnitTypeId)
                                            .ToList(),
                ListLocations = _context.Locations
                                    .Where(l => l.CurrentState > 0)
                                    .Include(l => l.Items)
                                    .Select(l => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                                    {
                                        Value = l.Id.ToString(),
                                        Text = "Room[ " + l.RoomNum + " ] → Case[ " + l.CaseNum + " ] → Shelf[ " + l.ShelfNum + " ] → Rack[ " + l.RackNum + " ] → Box[ " + l.BoxNum + " ] → Tube[ " + l.TubeNum + " ] "
                                    }).ToList(),

                NewLot = new Lot(),
                NewItem = new Item(),

            };
            Lots.CanCreateLot = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Lots.Create_Lots)).Succeeded;

            Lots.CanViewExpiryLots = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Lots.View_ExpieryLots)).Succeeded;

            Lots.CanViewDetails = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Lots.Details_Lots)).Succeeded;
            Lots.CanViewDetails = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Items.Details_Items)).Succeeded;

            Lots.CanEditLot = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Lots.Edit_Lots)).Succeeded;

            Lots.CanDeleteLot = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Lots.Delete_Lots)).Succeeded;

            Lots.CanExchangeLot = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Lots.Exchange_Lots)).Succeeded;

            Lots.CanPrintBarcode = (await _authorizationService
                .AuthorizeAsync(User, Permissions.Lots.Print_LotBarcode)).Succeeded;

            return View(Lots);
        }
        #endregion

        #region Delete Lot [Delete & Delete Log]
        //Delete
        [Authorize(Permissions.Lots.Delete_Lots)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);
            if (_servicesLot.Delete(Id))
                return RedirectToAction("index", "Lots");

            return RedirectToAction("index", "Lots");
        }


        ////Delete Log
        //[Authorize(Permissions.Centers.DeleteLog)]
        //public IActionResult DeleteLog(Guid Id)
        //{
        //    if (_servicesCenterLog.DeleteLog(Id))
        //        return RedirectToAction("Centers", "Centers");
        //    return RedirectToAction("Centers", "Centers");
        //}
        #endregion

        #region Add|Edit Lot [Create & Update]
        [Authorize(Permissions.Lots.Create_Lots), Authorize(Permissions.Lots.Edit_Lots)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(ItemViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            // model.NewLot.ItemId = model.NewItem.Id;
            // model.NewLot.Item = model.NewItem;
            if (model?.NewLot?.Id == 0)
            { //Create
                //Exist
                if (_servicesLot.FindBy(model.NewLot.LotNumber) != null)
                    SessionMsg(Helper.Error, "Exist Lot ", "This Lot already exists !");

                else
                {

                    if (_servicesLot.Save(model.NewLot))
                        SessionMsg(Helper.Success, "Add Lot", "The Lot has been added successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Adding Lot", "An error occurred while adding some data !");
                }
            }
            else
            { //Update
                if (_servicesLot.Save(model.NewLot))
                    SessionMsg(Helper.Success, "Edit Lot", "The Lot has been modified successfully !");
                else
                    SessionMsg(Helper.Error, "Error Editting Lot", "An error occurred while modifying some data !");

            }


            return RedirectToAction("index", "Lots");
        }
        #endregion
        #region Save & Print (Receive Lot screen → "Save & print label")
        [Authorize(Permissions.Lots.Create_Lots)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult SaveAndPrint(ItemViewModel model)
        {
            if (model?.NewLot == null)
                return RedirectToAction("Index", "Lots");

            if (model.NewLot.Id == 0 && _servicesLot.FindBy(model.NewLot.LotNumber) != null)
            {
                SessionMsg(Helper.Error, "Exist Lot ", "This Lot already exists !");
                return RedirectToAction("Index", "Lots");
            }

            if (!_servicesLot.Save(model.NewLot))
            {
                SessionMsg(Helper.Error, "Error Adding Lot", "An error occurred while adding some data !");
                return RedirectToAction("Index", "Lots");
            }

            SessionMsg(Helper.Success, "Add Lot", "The Lot has been added successfully !");

            // Id is set by EF after Save; fall back to lookup by lot number just in case
            var lotId = model.NewLot.Id != 0
                ? model.NewLot.Id
                : _context.Lots.Where(l => l.LotNumber == model.NewLot.LotNumber).Select(l => l.Id).FirstOrDefault();

            return RedirectToAction(nameof(PrintLot), new { id = lotId });
        }
        #endregion

        #region Details Of Lot 
        [HttpGet]
        public IActionResult SelectedLot(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            LotViewModel model = new()
            {
                NewLot = _servicesLot.FindBy(id),
                LotsList = _servicesLot.GetAll(),
                LotTransactionsList = _servicesOfLot.GetLotTransactionsOfLot(id).OrderBy(x => x.Move_Num).ToList()
            };
            //model.LocationData = _servicesOfLot.GetLocationDetailsOfLot(id);
            return View(model);
        }
        #endregion

        #region Receiving → Lot Creation 
        [HttpGet]


        public IActionResult CreateLot(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ItemViewModel model = new()
            {
                NewLot = new Lot(),
                NewItem = _servicesItem.FindBy(id),
                ItemsList = _servicesItem.GetAll(),
                LotsList = _servicesOfItem.GetLotsOfItem(id),
                ItemTransactionsList = _servicesOfItem.GetItemTransactionsOfItem(id),

                ListRooms = _context.Rooms
                                    .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name })
                                    .ToList(),

                ListSuppliers = _context.Suppliers
                                    .Where(s => s.CurrentState > 0)
                                    .Include(s => s.Lot)
                                    .Select(s => new SelectListItem
                                    {
                                        Value = s.Id.ToString(),
                                        Text = s.Name
                                    }).ToList(),
            };

            return View(model);
        }


        //public IActionResult CreateLot(int id)
        //{
        //    string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    LotViewModel model = new()
        //    {
        //        NewLot = _servicesLot.FindBy(id),
        //        LotsList = _servicesLot.GetAll()
        //    };
        //    return View(model);
        //}
        #endregion
        #region Disbursing → Lot Exchange 
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Exchange(ItemViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            bool isFullExchange =
                model.NewLot.ExchageQuantity == model.NewLot.AvilableQuantity;

            if (_servicesOfLot.Exchange(model.NewLot))
            {
                if (isFullExchange)
                {
                    SessionMsg(
                        Helper.Warning,
                        "Lot Removed",
                        "Lot quantity reached zero and was removed automatically."
                    );
                }
                else
                {
                    SessionMsg(
                        Helper.Success,
                        "Exchange Lot",
                        "The Lot has been exchanged successfully !"
                    );
                }
            }
            else
            {
                SessionMsg(
                    Helper.Error,
                    "Error Exchanging Lot",
                    "An error occurred while adding some data !"
                );
            }

            return RedirectToAction("Index", "Lots");
        }


        #endregion

        #region Print Lot Barcode
        // Legacy entry point (route values) — kept so old links keep working
        [Authorize(Permissions.Lots.Print_LotBarcode)]
        public IActionResult PrintLotBarcode(string barcodeFile, string itemName, string lotNumber, string expiryDate)
        {
            ViewBag.BarcodePath = barcodeFile;
            ViewBag.ItemName = itemName;
            ViewBag.LotNumber = lotNumber;
            ViewBag.ExpiryDate = DateTime.TryParse(expiryDate, out var d) ? d.ToString("dd/MM/yy") : "";
            return View("PrintLotBarcode");
        }

        // New entry point: everything loaded from the DB by id (name, storage, location, dates)
        [Authorize(Permissions.Lots.Print_LotBarcode)]
        public IActionResult PrintLot(int id)
        {
            var lot = _context.Lots
                .AsNoTracking()
                .Include(l => l.Item).ThenInclude(i => i.StorageUnitType)
                .Include(l => l.Box).ThenInclude(b => b.Shelf).ThenInclude(s => s.StorageUnit).ThenInclude(u => u.Room)
                .FirstOrDefault(l => l.Id == id);

            if (lot == null) return NotFound();

            ViewBag.BarcodePath = lot.BarcodeImage;
            ViewBag.ItemName = lot.Item?.Name;
            ViewBag.ItemCode = lot.Item?.Code;
            ViewBag.LotNumber = lot.LotNumber;
            ViewBag.ExpiryDate = lot.ExpiryDate?.ToString("dd/MM/yy") ?? "";
            ViewBag.ReceivedDate = lot.ReceivedDate?.ToString("dd/MM/yy") ?? "";
            ViewBag.Storage = lot.Item?.StorageUnitType?.Name;
            ViewBag.IsCold = lot.Item?.StorageUnitType?.TemperatureValue < 0;
            ViewBag.Location = lot.Box == null ? "" :
                $"{lot.Box.Shelf?.StorageUnit?.Name} › {lot.Box.Name} · R{lot.RowIndex}C{lot.ColumnIndex}";

            return View("PrintLotBarcode");
        }
        #endregion
    }
}
