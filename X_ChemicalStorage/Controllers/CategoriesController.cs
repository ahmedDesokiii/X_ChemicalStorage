using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.IRepository;

namespace X_ChemicalStorage.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly IServicesRepository<Category> _servicesCategory ;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CategoriesController(IServicesRepository<Category> servicesCategory, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _servicesCategory = servicesCategory;
            _userManager = userManager;
            _context = context;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of Categories
        [Authorize(Permissions.Categories.View_Categories)]
        public IActionResult Index()
        {
            var Categories = new CategoryViewModel
            {
                CategoriesList = _servicesCategory.GetAll(),
                NewCategory = new Category()
            };
            return View(Categories);
        }
        #endregion

        #region Delete Category [Delete & Delete Log]
        //Delete
        [Authorize(Permissions.Categories.Delete_Categories)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);
            if (_servicesCategory.Delete(Id))
                return RedirectToAction("index", "Categories");

            return RedirectToAction("index", "Categories");
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
        [Authorize(Permissions.Categories.Create_Categories), Authorize(Permissions.Categories.Edit_Categories)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

                if (model?.NewCategory?.Id == 0)
                { //Create
                    //Exist
                    if (_servicesCategory.FindBy(model.NewCategory.Name) != null)
                        SessionMsg(Helper.Error, "فئة مكرره !", "اسم الفئة موجود من قبل");

                    else
                    {
                        if (_servicesCategory.Save(model.NewCategory))
                            SessionMsg(Helper.Success, "تم الإضافة !", "تم اضافة الفئة بنجاح ");
                        else
                            SessionMsg(Helper.Error, "خطأ في الإضافة", "حدوث خطأ اثناء ادخال بعض البيانات !");
                    }
                }
                else
                { //Update
                    if (_servicesCategory.Save(model.NewCategory))
                        SessionMsg(Helper.Success, "تم التعديل", "تم تعديل بيانات الفئة بنجاح !");
                    else
                        SessionMsg(Helper.Error, "مشكلة في التعديل", "! حدوث خطأ اثناء تعديل بعض البيانات");

                }
            }
            return RedirectToAction("index", "Categories");
        }
        #endregion

        #region Details Of Category 
        [HttpGet]
        public IActionResult SelectedCategory(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            CategoryViewModel model = new()
            {
                NewCategory = _servicesCategory.FindBy(id),
                CategoriesList = _servicesCategory.GetAll()
            };
            return View(model);
        }
        #endregion
        
    }
}
