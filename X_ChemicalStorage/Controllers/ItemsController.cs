using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using X_ChemicalStorage.IRepository;
using ZXing;
using ZXing.Common;

namespace X_ChemicalStorage.Controllers
{
    public class ItemsController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IServicesRepository<Item> _servicesItem;
        private readonly IServicesRepository<Lot> _servicesLot;
        private readonly IServicesItem _servicesOfItem;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ItemsController(IWebHostEnvironment env, IServicesRepository<Item> servicesItem, IServicesRepository<Lot> servicesLot, IServicesItem servicesOfItem, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _env = env;
            _servicesItem = servicesItem;
            _servicesLot = servicesLot;
            _servicesOfItem = servicesOfItem;
            _userManager = userManager;
            _context = context;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of Items
        [Authorize(Permissions.Items.View_Items)]
        public IActionResult Index()
        {
            var Items = new ItemViewModel();

            Items.ItemsList = _servicesItem.GetAll()
                                            .OrderByDescending(x => x.StorageUnitType.Name)
                                            .ToList();
            Items.NewItem = new Item();

            Items.ListCategories = _context.Categories
                                .Where(c => c.CurrentState > 0)
                                .Include(l => l.Items)
                                .Select(c => new SelectListItem
                                {
                                    Value = c.Id.ToString(),
                                    Text = c.Name
                                }).ToList();

           Items.ListStorageUnitTypes = _context.StorageUnitTypes
                                .Select(s => new SelectListItem
                                {
                                    Value = s.Id.ToString(),
                                    Text = s.Name
                                }).ToList();

            Items.ListUnits = _context.Units
                                .Where(l => l.CurrentState > 0)
                                .Include(l => l.Items)
                                .Select(l => new SelectListItem
                                {
                                    Value = l.Id.ToString(),
                                    Text = l.Name
                                }).ToList();

            return View(Items);
        }
        #endregion

        #region Delete Item [Delete & Delete Log]
        [Authorize(Permissions.Items.Delete_Items)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);
            if (_servicesItem.Delete(Id))
                return RedirectToAction("index", "Items");

            return RedirectToAction("index", "Items");
        }
        #endregion

        #region Add|Edit Item [Create & Update] without print
        [Authorize(Permissions.Items.Create_Items), Authorize(Permissions.Items.Edit_Items)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(ItemViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            if (model?.NewItem?.Id == 0)
            {
                if (_servicesItem.FindBy(model.NewItem.Name) != null)
                    SessionMsg(Helper.Error, "Exist Item ", "This Item Name already exists !");
                else
                {
                    if (_servicesItem.Save(model.NewItem))
                        SessionMsg(Helper.Success, "Add Item", "The Item has been added successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Adding Item", "An error occurred while adding some data !");
                }
            }
            else
            {
                if (_servicesItem.Save(model.NewItem))
                    SessionMsg(Helper.Success, "Edit Item", "The Item has been modified successfully !");
                else
                    SessionMsg(Helper.Error, "Error Editting Item", "An error occurred while modifying some data !");
            }
            return RedirectToAction("index", "Items");
        }
        #endregion

        #region Add with print |Edit Item [Create with print & Update]
        [Authorize(Permissions.Items.Create_Items), Authorize(Permissions.Items.Edit_Items)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult SaveAndPrint(ItemViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            if (model?.NewItem?.Id == 0)
            {
                if (_servicesItem.FindBy(model.NewItem.Name) != null)
                    SessionMsg(Helper.Error, "Exist Item ", "This Item Name already exists !");
                else
                {
                    if (_servicesItem.Save(model.NewItem))
                    {
                        SessionMsg(Helper.Success, "Add Item", "The Item has been added successfully !");
                        return RedirectToAction("PrintItemBarcode", "Items", new { barcodeFile = model.NewItem.BarcodeImage, code = model.NewItem.Code });
                    }
                    else
                        SessionMsg(Helper.Error, "Error Adding Item", "An error occurred while adding some data !");
                }
            }
            else
            {
                if (_servicesItem.Save(model.NewItem))
                {
                    SessionMsg(Helper.Success, "Edit Item", "The Item has been modified successfully !");
                    return RedirectToAction("PrintItemBarcode", "Items", new { barcodeFile = model.NewItem.BarcodeImage, code = model.NewItem.Code });
                }
                else
                    SessionMsg(Helper.Error, "Error Editting Item", "An error occurred while modifying some data !");
            }
            return RedirectToAction("index", "Items");
        }
        #endregion

        #region Details Of Item
        [HttpGet]
        public IActionResult SelectedItem(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var todayUtc = DateTime.UtcNow.Date;
            ItemViewModel model = new()
            {
                NewItem = _servicesItem.FindBy(id),
                ItemsList = _servicesItem.GetAll()
                                            .OrderByDescending(x => x.StorageUnitType.Name)
                                            .ToList(),
                LotsList = _servicesOfItem.GetLotsOfItem(id)
                                        .OrderBy(x => x.ExpiryDate)
                                        .ThenBy(x => x.ManufactureDate)
                                        .ThenBy(x => x.ReceivedDate)
                                        .ToList(),

                ExpieredLots = _servicesLot.GetAll()
                                        .Where(x => x.ItemId == id && x.ExpiryDate < todayUtc && x.CurrentState > 0)
                                        .OrderBy(x => x.ExpiryDate)
                                        .ThenBy(x => x.ManufactureDate)
                                        .ThenBy(x => x.ReceivedDate)
                                        .ToList(),

                ItemTransactionsList = _servicesOfItem.GetItemTransactionsOfItem(id).OrderBy(x => x.Move_Num).ToList(),
            };

            // ✅ بدل GetLocationDetailsOfItem القديمة
            model.RequiredStorageType = _servicesOfItem.GetRequiredStorageConditionOfItem(id);

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

                // ✅ بدل ListLocations القديمة - أول خطوة في الكاسكيد
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
        #endregion

        #region Disbursing → Lot Exchange
        [HttpGet]
        public IActionResult ExchangeLot(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var item = _context.Items.FirstOrDefault(x => x.Id == id);
            if (item == null) return NotFound();

            var lots = _context.Lots
                .Where(x => x.ItemId == id && x.AvilableQuantity > 0)
                .OrderBy(x => x.ExpiryDate)
                .ToList();

            var newLot = lots.FirstOrDefault();

            var listLotsOfItem = lots.Select(lot => new SelectListItem
            {
                Value = lot.Id.ToString(),
                Text = $"Exp Date [{lot.ExpiryDate:yyyy-MM-dd}] → Available Qty [{lot.AvilableQuantity}]  ",
                Selected = (newLot != null && lot.Id == newLot.Id)
            }).ToList();

            var model = new ItemViewModel
            {
                NewItem = item,
                LotsList = lots,
                NewLot = newLot,
                ListLotsOfItem = listLotsOfItem
            };

            return View(model);
        }
        #endregion

        #region Print Barcode
        public IActionResult PrintItemBarcode(string barcodeFile, string code)
        {
            ViewBag.BarcodePath = "/barcodes/" + code + ".png";
            ViewBag.Code = code;
            return View();
        }
        #endregion
    }
}