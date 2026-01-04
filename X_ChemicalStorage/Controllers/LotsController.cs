using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.IRepository;

namespace X_ChemicalStorage.Controllers
{
    public class LotsController : Controller
    {
        private readonly IServicesRepository<Lot> _servicesLot ;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LotsController(IServicesRepository<Lot> servicesLot, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _servicesLot = servicesLot;
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
            var Lots = new LotViewModel
            {
                LotsList = _servicesLot.GetAll(),
                NewLot = new Lot()
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
        public IActionResult Save(LotViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

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
                LotsList = _servicesLot.GetAll()
            };
            return View(model);
        }
        #endregion
        
    }
}
