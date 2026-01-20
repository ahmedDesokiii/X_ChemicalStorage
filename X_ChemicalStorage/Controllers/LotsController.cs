using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using X_ChemicalStorage.IRepository;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.Controllers
{
    public class LotsController : Controller
    {
        private readonly IServicesRepository<Lot> _servicesLot ;
        private readonly IServicesLot _servicesOfLot ;
        private readonly IServicesRepository<Item> _servicesItem ;
        private readonly IInventoryService _inventoryService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LotsController(IServicesRepository<Lot> servicesLot, IServicesLot servicesOfLot, IServicesRepository<Item> servicesItem, IInventoryService inventoryService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _servicesLot = servicesLot;
            _servicesOfLot = servicesOfLot;
            _servicesItem = servicesItem;
            _inventoryService = inventoryService;
            _userManager = userManager;
            _context = context;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of Lots
        [Authorize(Permissions.Lots.View_Lots)]
        public IActionResult Index()
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
                                            .Where(x=>x.ExpiryDate < todayUtc)
                                            .OrderBy(x => x.ExpiryDate)
                                            .ThenBy(x => x.ManufactureDate)
                                            .ThenBy(x => x.ReceivedDate)
                                            .ToList(),

                ItemsList = _servicesItem.GetAll()
                                            .OrderByDescending(x => x.StorageCondition)
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
        #region Add|Edit Lot [Create & Update]
        [Authorize(Permissions.Lots.Create_Lots), Authorize(Permissions.Lots.Edit_Lots)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult SaveAndPrint(LotViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

                if (model?.NewLot?.Id == 0)
                { //Create
                    //Exist
                    if (_servicesLot.FindBy(model.NewLot.LotNumber.ToString()) != null)
                        SessionMsg(Helper.Error, "Exist Lot ", "This Lot already exists !");

                    else
                    {
                        if (_servicesLot.Save(model.NewLot)) { 
                            SessionMsg(Helper.Success, "Add Lot", "The Lot has been added successfully !");
                            return RedirectToAction("PrintItemBarcode", "Lots", new { barcodeFile = model.NewLot.BarcodeImage });
                        }
                            
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
            }
            return RedirectToAction("index", "Lots");
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
            model.LocationData = _servicesOfLot.GetLocationDetailsOfLot(id);
            return View(model);
        }
        #endregion

        #region Receiving → Lot Creation 
        [HttpGet]
        public IActionResult CreateLot(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            LotViewModel model = new()
            {
                NewLot = _servicesLot.FindBy(id),
                LotsList = _servicesLot.GetAll()
            };
            return View(model);
        }
        #endregion


        // View
        public IActionResult Scan()
        {
            return View();
        }

        // AJAX
        [HttpGet]
        public async Task<IActionResult> GetLotsByBarcode(string barcode)
        {
            var lots = await _inventoryService.GetLotsByBarcodeAsync(barcode);

            if (!lots.Any())
                return NotFound("لا توجد Lots متاحة");

            return Json(lots);
        }

        [HttpPost]
        public IActionResult Submit(int lotId, int quantity)
        {
            // خصم الكمية ومعالجة العملية
            return Ok();
        }

        #region Print Lot Barcode
        public IActionResult PrintLotBarcode(string barcodeFile, string itemName , string lotNumber, string expiryDate)
        {
            ViewBag.BarcodePath = barcodeFile;
            ViewBag.ItemName = itemName;
            ViewBag.LotNumber = lotNumber;
            ViewBag.ExpiryDate = Convert.ToDateTime(expiryDate).ToShortDateString();
            return View();
        }
        #endregion
    }
}
