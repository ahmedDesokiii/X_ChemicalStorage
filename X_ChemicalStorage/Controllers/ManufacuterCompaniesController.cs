using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.IRepository;

namespace X_ChemicalStorage.Controllers
{
    public class ManufacuterCompaniesController : Controller
    {
        private readonly IServicesRepository<ManufacuterCompany> _servicesManufacuterCompany ;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ManufacuterCompaniesController(IServicesRepository<ManufacuterCompany> servicesManufacuterCompany, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _servicesManufacuterCompany = servicesManufacuterCompany;
            _userManager = userManager;
            _context = context;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of ManufacuterCompanies
        [Authorize(Permissions.Manufacuters.View_Manufacuters)]
        public IActionResult Index()
        {
            var ManufacuterCompanies = new ManufacuterCompanyViewModel
            {
                ManufacuterCompaniesList = _servicesManufacuterCompany.GetAll(),
                NewManufacuterCompany = new ManufacuterCompany()
            };
            return View(ManufacuterCompanies);
        }
        #endregion

        #region Delete ManufacuterCompanies [Delete & Delete Log]
        //Delete
        [Authorize(Permissions.Manufacuters.Delete_Manufacuters)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);
            if (_servicesManufacuterCompany.Delete(Id))
                return RedirectToAction("index", "ManufacuterCompanies");

            return RedirectToAction("index", "ManufacuterCompanies");
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

        #region Add|Edit ManufacuterCompanies [Create & Update]
        [Authorize(Permissions.Manufacuters.Create_Manufacuters), Authorize(Permissions.Manufacuters.Edit_Manufacuters)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(ManufacuterCompanyViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

                if (model?.NewManufacuterCompany?.Id == 0)
                { //Create
                    //Exist
                    if (_servicesManufacuterCompany.FindBy(model.NewManufacuterCompany.Name) != null)
                        SessionMsg(Helper.Error, "Exist Manufacturer ", "This Manufacturer already exists !");

                    else
                    {
                        if (_servicesManufacuterCompany.Save(model.NewManufacuterCompany))
                            SessionMsg(Helper.Success, "Add Manufacturer", "The Manufacturer has been added successfully !");
                        else
                            SessionMsg(Helper.Error, "Error Adding Manufacturer", "An error occurred while adding some data !");
                    }
                }
                else
                { //Update
                    if (_servicesManufacuterCompany.Save(model.NewManufacuterCompany))
                        SessionMsg(Helper.Success, "Edit Manufacturer", "The Manufacturer has been modified successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Editting Manufacturer", "An error occurred while modifying some data !");

                }
            }
            return RedirectToAction("index", "ManufacuterCompanies");
        }
        #endregion

        #region Details Of ManufacuterCompany 
        [HttpGet]
        public IActionResult SelectedManufacuterCompany(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ManufacuterCompanyViewModel model = new()
            {
                NewManufacuterCompany = _servicesManufacuterCompany.FindBy(id),
                ManufacuterCompaniesList = _servicesManufacuterCompany.GetAll()
            };
            return View(model);
        }
        #endregion
        
    }
}
