using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.IRepository;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.ViewModels;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly IServicesRepository<Category> _servicesCategory;
        private readonly IServicesRepository<SubCategory> _servicesSubCategory; // NEW
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CategoriesController(
            IServicesRepository<Category> servicesCategory,
            IServicesRepository<SubCategory> servicesSubCategory,   // NEW
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _servicesCategory = servicesCategory;
            _servicesSubCategory = servicesSubCategory;
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
                NewCategory = new Category(),

                // NEW — powers the "Category -> SubCategories" tree UI (spec section 10)
                SubCategoriesList = _servicesSubCategory.GetAll(),
                NewSubCategory = new SubCategory()
            };
            return View(Categories);
        }
        #endregion

        #region Delete Category [Delete & Delete Log]
        [Authorize(Permissions.Categories.Delete_Categories)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);

            // Guard: don't silently orphan SubCategories when a Category is soft-deleted.
            bool hasSubCategories = _context.SubCategories.Any(sc => sc.CategoryId == Id && sc.CurrentState > 0);
            if (hasSubCategories)
            {
                SessionMsg(Helper.Error, "Cannot Delete Category", "This category still has subcategories and cannot be deleted !");
                return RedirectToAction("index", "Categories");
            }

            if (_servicesCategory.Delete(Id))
                return RedirectToAction("index", "Categories");

            return RedirectToAction("index", "Categories");
        }
        #endregion

        #region Add|Edit Category [Create & Update]
        [Authorize(Permissions.Categories.Create_Categories),
         Authorize(Permissions.Categories.Edit_Categories)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                if (model?.NewCategory?.Id == 0)
                { //Create
                    if (_servicesCategory.FindBy(model.NewCategory.Name) != null)
                        SessionMsg(Helper.Error, "Exist Category ", "This category already exists !");
                    else
                    {
                        if (_servicesCategory.Save(model.NewCategory))
                            SessionMsg(Helper.Success, "Add Category", "The category has been added successfully !");
                        else
                            SessionMsg(Helper.Error, "Error Adding Category", "An error occurred while adding some data !");
                    }
                }
                else
                { //Update
                    if (_servicesCategory.Save(model.NewCategory))
                        SessionMsg(Helper.Success, "Edit Category", "The category has been modified successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Editting Category", "An error occurred while modifying some data !");
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
                CategoriesList = _servicesCategory.GetAll(),
                SubCategoriesList = _servicesSubCategory.GetAll() // NEW
            };
            return View(model);
        }
        #endregion
    }
}
