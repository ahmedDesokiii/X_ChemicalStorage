using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.IRepository;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.ViewModels;
using static X_ChemicalStorage.Constants.Permissions;

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

        public ItemsController(IWebHostEnvironment env,
            IServicesRepository<Item> servicesItem, IServicesRepository<Lot> servicesLot,
            IServicesItem servicesOfItem, UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
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

            // NEW — left empty on purpose: the Add form's SubCategory dropdown starts
            // disabled and is populated client-side via GetSubCategoriesByCategory
            // once a Category is chosen (see wwwroot/js/subcategory-cascade.js).
            Items.ListSubCategories = new List<SelectListItem>();

            Items.ListStorageUnitTypes = _context.StorageUnitTypes
                                .Select(s => new SelectListItem
                                {
                                    Value = s.Id.ToString(),
                                    Text = s.Name
                                })
                                .OrderByDescending(x=>x.Value)
                                .ToList();
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
        [Authorize(Permissions.Items.Create_Items),
         Authorize(Permissions.Items.Edit_Items)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(ItemViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            // ===== NEW — server-side SubCategory/Category integrity check =====
            // Never trust the dropdown values alone (spec section 7).
            if (model?.NewItem?.SubCategoryId != null)
            {
                var subCategory = _context.SubCategories
                    .FirstOrDefault(sc => sc.Id == model.NewItem.SubCategoryId && sc.CurrentState > 0);

                if (subCategory == null || subCategory.CategoryId != model.NewItem.CategoryId)
                {
                    SessionMsg(Helper.Error, "Invalid SubCategory",
                        "The selected subcategory does not belong to the selected category !");
                    return RedirectToAction("index", "Items");
                }
            }
            // ====================================================================

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
        [Authorize(Permissions.Items.Create_Items),
         Authorize(Permissions.Items.Edit_Items)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult SaveAndPrint(ItemViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            // Same server-side SubCategory/Category integrity check as Save()
            if (model?.NewItem?.SubCategoryId != null)
            {
                var subCategory = _context.SubCategories
                    .FirstOrDefault(sc => sc.Id == model.NewItem.SubCategoryId && sc.CurrentState > 0);

                if (subCategory == null || subCategory.CategoryId != model.NewItem.CategoryId)
                {
                    SessionMsg(Helper.Error, "Invalid SubCategory",
                        "The selected subcategory does not belong to the selected category !");
                    return RedirectToAction("index", "Items");
                }
            }

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

            var item = _servicesItem.FindBy(id);

            ItemViewModel model = new()
            {
                NewItem = item,
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

            // NEW — Edit behaves exactly like Add (spec section 8): pre-load the
            // SubCategories belonging to this item's current Category so the
            // dropdown is already populated and the current value pre-selected.
            if (item != null)
            {
                model.ListSubCategories = _context.SubCategories
                    .AsNoTracking()
                    .Where(sc => sc.CategoryId == item.CategoryId && sc.CurrentState > 0)
                    .OrderBy(sc => sc.Name)
                    .Select(sc => new SelectListItem
                    {
                        Value = sc.Id.ToString(),
                        Text = sc.Name,
                        Selected = sc.Id == item.SubCategoryId
                    }).ToList();
            }

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

        #region SubCategories by Category (cascade dropdown)

        [HttpGet]
        public IActionResult GetSubCategoriesByCategory(int categoryId)
        {
            var list = _context.SubCategories
                .AsNoTracking()
                .Where(sc => sc.CategoryId == categoryId && sc.CurrentState > 0)
                .OrderBy(sc => sc.Name)
                .Select(sc => new
                {
                    id = sc.Id,
                    name = sc.Name
                })
                .ToList();

            return Json(list);
        }
        //[HttpGet]
        //public IActionResult GetSubCategoriesByCategory(int categoryId)
        //{
        //    var list = _context.SubCategories
        //        .Where(sc => sc.CategoryId == categoryId && sc.CurrentState > 0)
        //        .OrderBy(sc => sc.Name)
        //        .Select(sc => new SelectListItem
        //        {
        //            Value = sc.Id.ToString(),
        //            Text = sc.Name
        //        }).ToList();

        //    return Json(list);
        //}
        #endregion
    }
}
