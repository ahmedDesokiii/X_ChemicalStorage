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
        [Authorize(Permissions.ManufacuterCompanies.View_ManufacuterCompanies)]
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
        [Authorize(Permissions.ManufacuterCompanies.Delete_ManufacuterCompanies)]
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

        #region Add|Edit Center [Create & Update]
        [Authorize(Permissions.ManufacuterCompanies.Create_ManufacuterCompanies), Authorize(Permissions.ManufacuterCompanies.Edit_ManufacuterCompanies)]
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
                        SessionMsg(Helper.Error, "شركة مصنعة مكررة !", "اسم الشركة المصنعة موجود من قبل");

                    else
                    {
                        if (_servicesManufacuterCompany.Save(model.NewManufacuterCompany))
                            SessionMsg(Helper.Success, "تم الإضافة !", "تم اضافة الشركة المصنعة بنجاح ");
                        else
                            SessionMsg(Helper.Error, "خطأ في الإضافة", "حدوث خطأ اثناء ادخال بعض البيانات !");
                    }
                }
                else
                { //Update
                    if (_servicesManufacuterCompany.Save(model.NewManufacuterCompany))
                        SessionMsg(Helper.Success, "تم التعديل", "تم تعديل بيانات الشركة المصنعة بنجاح !");
                    else
                        SessionMsg(Helper.Error, "مشكلة في التعديل", "! حدوث خطأ اثناء تعديل بعض البيانات");

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
