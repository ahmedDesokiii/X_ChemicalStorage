using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.IRepository;

namespace X_ChemicalStorage.Controllers
{
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
        [Authorize(Permissions.Locations.View_Locations)]
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
        [Authorize(Permissions.Locations.Delete_Locations)]
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
        [Authorize(Permissions.Locations.Create_Locations), Authorize(Permissions.Locations.Edit_Locations)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(LocationViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

                if (model?.NewLocation?.Id == 0)
                { //Create
                    //Exist
                    if (_servicesLocation.FindBy(model.NewLocation.LocationName) != null)
                        SessionMsg(Helper.Error, "فئة مكرره !", "اسم الفئة موجود من قبل");

                    else
                    {
                        if (_servicesLocation.Save(model.NewLocation))
                            SessionMsg(Helper.Success, "تم الإضافة !", "تم اضافة الفئة بنجاح ");
                        else
                            SessionMsg(Helper.Error, "خطأ في الإضافة", "حدوث خطأ اثناء ادخال بعض البيانات !");
                    }
                }
                else
                { //Update
                    if (_servicesLocation.Save(model.NewLocation))
                        SessionMsg(Helper.Success, "تم التعديل", "تم تعديل بيانات الفئة بنجاح !");
                    else
                        SessionMsg(Helper.Error, "مشكلة في التعديل", "! حدوث خطأ اثناء تعديل بعض البيانات");

                }
            }
            return RedirectToAction("index", "Locations");
        }
        #endregion

        #region Details Of Location 
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
