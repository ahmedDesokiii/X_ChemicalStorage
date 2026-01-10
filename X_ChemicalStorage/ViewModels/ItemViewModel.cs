using Microsoft.AspNetCore.Mvc.Rendering;

namespace X_ChemicalStorage.ViewModels
{
    public class ItemViewModel
    {
        public Item? NewItem { get; set; } = new Item();
        public List<Item>? ItemsList { get; set; } = new List<Item>();

        // Add other properties as needed for the ViewModel
        public IEnumerable<SelectListItem>? ListUnits { get; set; }
        public IEnumerable<SelectListItem>? ListCategories { get; set; }
        public IEnumerable<SelectListItem>? ListLocations { get; set; }
    }
}
