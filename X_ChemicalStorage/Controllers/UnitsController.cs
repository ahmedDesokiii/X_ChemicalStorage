using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.IRepository;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
    public class UnitsController : Controller
    {
        private readonly IServicesRepository<Unit> _servicesUnit ;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UnitsController(IServicesRepository<Unit> servicesUnit, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _servicesUnit = servicesUnit;
            _userManager = userManager;
            _context = context;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of Units
        [HasPermission(Permissions.Units.View)]
        public IActionResult Index()
        {
            var Units = new UnitViewModel
            {
                UnitsList = _servicesUnit.GetAll().OrderBy(x => x.Name).ToList(),
                NewUnit = new Unit()
            };
            return View(Units);
        }
        #endregion

        #region Delete Unit [Delete & Delete Log]
        //Delete
        [HasPermission(Permissions.Units.Delete)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);
            if (_servicesUnit.Delete(Id))
                return RedirectToAction("index", "Units");

            return RedirectToAction("index", "Units");
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

        #region Add|Edit Unit [Create & Update]
        [HasPermission(Permissions.Units.Create, Permissions.Units.Edit)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Save(UnitViewModel model)
        {
            // Same form creates and edits → check the specific permission
            if (!await HttpContext.HasPermissionAsync((model?.NewUnit?.Id ?? 0) == 0 ? Permissions.Units.Create : Permissions.Units.Edit))
                return Forbid();

            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

                if (model?.NewUnit?.Id == 0)
                { //Create
                    //Exist
                    if (_servicesUnit.FindBy(model.NewUnit.Name) != null)
                        SessionMsg(Helper.Error, "Exist Unit ", "This Unit already exists !");

                    else
                    {
                        if (_servicesUnit.Save(model.NewUnit))
                            SessionMsg(Helper.Success, "Add Unit", "The Unit has been added successfully !");
                        else
                            SessionMsg(Helper.Error, "Error Adding Unit", "An error occurred while adding some data !");
                    }
                }
                else
                { //Update
                    if (_servicesUnit.Save(model.NewUnit))
                        SessionMsg(Helper.Success, "Edit Unit", "The Unit has been modified successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Editting Unit", "An error occurred while modifying some data !");

                }
            }
            return RedirectToAction("index", "Units");
        }
        #endregion

        #region Details Of Unit 
        [HasPermission(Permissions.Units.Details, Permissions.Units.Edit)]
        [HttpGet]
        public IActionResult SelectedUnit(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            UnitViewModel model = new()
            {
                NewUnit = _servicesUnit.FindBy(id),
                UnitsList = _servicesUnit.GetAll()
            };
            return View(model);
        }
        #endregion
        
    }
}
