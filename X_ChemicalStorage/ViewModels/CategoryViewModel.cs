namespace X_ChemicalStorage.ViewModels
{
    public class CategoryViewModel
    {
        public Category? NewCategory { get; set; } = new Category();
        public List<Category>? CategoriesList { get; set; } = new List<Category>();
    }
}
