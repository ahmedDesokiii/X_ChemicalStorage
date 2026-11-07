using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.IRepository;
using X_ChemicalStorage.IRepository.ServicesRepository;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
    public class LocationsController : Controller
    {
        private readonly IServicesRepository<Location> _servicesLocation ;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LocationsController(IServicesRepository<Location> servicesLocation, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _servicesLocation = servicesLocation;
            _userManager = userManager;
            _context = context;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of Locations
        [HasPermission(Permissions.Locations.View)]
        public IActionResult Index()
        {
            var Locations = new LocationViewModel
            {
                LocationsList = _servicesLocation.GetAll(),
                NewLocation = new Location()
            };
            return View(Locations);
        }
        #endregion

        #region Delete Location [Delete & Delete Log]
        //Delete
        [HasPermission(Permissions.Locations.Delete)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);
            if (_servicesLocation.Delete(Id))
                return RedirectToAction("index", "Locations");

            return RedirectToAction("index", "Locations");
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

        #region Add|Edit Center [Create & Update]
        [HasPermission(Permissions.Locations.Create, Permissions.Locations.Edit)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Save(LocationViewModel model)
        {
            // Same form creates and edits → check the specific permission
            if (!await HttpContext.HasPermissionAsync((model?.NewLocation?.Id ?? 0) == 0 ? Permissions.Locations.Create : Permissions.Locations.Edit))
                return Forbid();

            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

                if (model?.NewLocation?.Id == 0)
                { //Create
                    //Exist
                    //if (_servicesLocation.FindBy(model.NewLocation.RoomNum.ToString() != null)
                    //    SessionMsg(Helper.Error, "Exist Location ", "This Location Name already exists !");

                    //else
                    //{
                        if (_servicesLocation.Save(model.NewLocation))
                            SessionMsg(Helper.Success, "Add Location", "The Location has been added successfully !");
                        else
                            SessionMsg(Helper.Error, "Error Adding Location", "An error occurred while adding some data !");
                    //}
                }
                else
                { //Update
                    if (_servicesLocation.Save(model.NewLocation))
                        SessionMsg(Helper.Success, "Edit Location", "The Location has been modified successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Editting Location", "An error occurred while modifying some data !");
                }
            }
            return RedirectToAction("index", "Locations");
        }
        #endregion

        #region Details Of Location 
        [HasPermission(Permissions.Locations.Details, Permissions.Locations.Edit)]
        [HttpGet]
        public IActionResult SelectedLocation(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            LocationViewModel model = new()
            {
                NewLocation = _servicesLocation.FindBy(id),
                LocationsList = _servicesLocation.GetAll()
            };
            return View(model);
        }
        #endregion
        
    }
}
