using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.IRepository;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.ViewModels;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.Controllers
{
    // Managed from within the Categories screen (see spec section 10), so it
    // reuses the existing Permissions.Categories.* constants rather than
    // introducing a brand-new permission module the admin would have to
    // re-assign to every role.
    [Authorize]
    public class SubCategoriesController : Controller
    {
        private readonly IServicesRepository<SubCategory> _servicesSubCategory;
        private readonly IServicesSubCategory _servicesSubCategoryExtra;
        private readonly IServicesRepository<Category> _servicesCategory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SubCategoriesController(
            IServicesRepository<SubCategory> servicesSubCategory,
            IServicesSubCategory servicesSubCategoryExtra,
            IServicesRepository<Category> servicesCategory,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _servicesSubCategory = servicesSubCategory;
            _servicesSubCategoryExtra = servicesSubCategoryExtra;
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

        #region Add|Edit SubCategory  [Create & Update]
        [HasPermission(Permissions.Categories.Create, Permissions.Categories.Edit)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Save(SubCategoryViewModel model)
        {
            // Same form creates and edits → check the specific permission
            if (!await HttpContext.HasPermissionAsync((model?.NewSubCategory?.Id ?? 0) == 0 ? Permissions.Categories.Create : Permissions.Categories.Edit))
                return Forbid();

            if (ModelState.IsValid && model?.NewSubCategory != null)
            {
                // Server-side truth: the parent Category must actually exist.
                var parentCategory = _servicesCategory.FindBy(model.NewSubCategory.CategoryId);
                if (parentCategory == null)
                {
                    SessionMsg(Helper.Error, "Invalid Category", "The selected category does not exist.");
                    return RedirectToAction("index", "Categories");
                }

                if (model.NewSubCategory.Id == 0)
                {
                    // Exist? (same name within the same Category)
                    bool exists = _context.SubCategories.Any(sc =>
                        sc.CategoryId == model.NewSubCategory.CategoryId &&
                        sc.Name == model.NewSubCategory.Name.Trim() &&
                        sc.CurrentState > 0);

                    if (exists)
                        SessionMsg(Helper.Error, "Exist SubCategory", "This subcategory already exists under the selected category !");
                    else if (_servicesSubCategory.Save(model.NewSubCategory))
                        SessionMsg(Helper.Success, "Add SubCategory", "The subcategory has been added successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Adding SubCategory", "An error occurred while adding some data !");
                }
                else
                {
                    if (_servicesSubCategory.Save(model.NewSubCategory))
                        SessionMsg(Helper.Success, "Edit SubCategory", "The subcategory has been modified successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Editing SubCategory", "An error occurred while modifying some data !");
                }
            }
            return RedirectToAction("index", "Categories");
        }
        #endregion

        #region Delete SubCategory (blocked if Items still depend on it)
        [HasPermission(Permissions.Categories.Delete)]
        public IActionResult Delete(int Id)
        {
            if (_servicesSubCategory.Delete(Id))
                SessionMsg(Helper.Success, "Delete SubCategory", "The subcategory has been deleted successfully !");
            else
                SessionMsg(Helper.Error, "Cannot Delete SubCategory", "This subcategory still has items assigned to it and cannot be deleted !");

            return RedirectToAction("index", "Categories");
        }
        #endregion

        #region Cascading AJAX Endpoint — used by Items/Create & Items/Edit
        /// <summary>
        /// Returns only the active SubCategories belonging to the given Category.
        /// Never trusts the caller beyond the categoryId itself — invalid/missing
        /// ids simply return an empty list rather than throwing.
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetSubCategoriesByCategory(int categoryId)
        {
            if (categoryId <= 0)
                return Json(Array.Empty<object>());

            var subCategories = await _context.SubCategories
                .AsNoTracking()
                .Where(sc => sc.CategoryId == categoryId && sc.CurrentState > 0)
                .OrderBy(sc => sc.Name)
                .Select(sc => new { sc.Id, sc.Name })
                .ToListAsync();

            return Json(subCategories);
        }
        #endregion
    }
}
