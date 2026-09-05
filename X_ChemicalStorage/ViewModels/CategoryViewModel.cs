using Microsoft.AspNetCore.Mvc.Rendering;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.ViewModels
{
    public class CategoryViewModel
    {
        public Category? NewCategory { get; set; } = new Category();
        public List<Category>? CategoriesList { get; set; } = new List<Category>();

        public IEnumerable<SelectListItem>? ListCategories { get; set; }

        // NEW — lets the Category screen manage SubCategories inline (see section 10 of the spec)
        public SubCategory? NewSubCategory { get; set; } = new SubCategory();
        public List<SubCategory>? SubCategoriesList { get; set; } = new List<SubCategory>();
    }
}
