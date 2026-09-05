using Microsoft.AspNetCore.Mvc.Rendering;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.ViewModels
{
    public class SubCategoryViewModel
    {
        public SubCategory? NewSubCategory { get; set; } = new SubCategory();
        public List<SubCategory>? SubCategoriesList { get; set; } = new List<SubCategory>();
        public IEnumerable<SelectListItem>? ListCategories { get; set; }
    }
}
