using Microsoft.AspNetCore.Mvc.Rendering;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.ViewModels
{
    public class ItemViewModel
    {
        public Item? NewItem { get; set; } = new Item();
        public List<Item>? ItemsList { get; set; } = new List<Item>();
        public List<Lot>? LotsList { get; set; } = new List<Lot>();
        public List<Lot>? ExpieredLots { get; set; } = new List<Lot>();
        public List<ItemTransaction>? ItemTransactionsList { get; set; } = new List<ItemTransaction>();
        public Location? LocationData { get; set; } = new Location();
        // Add other properties as needed for the ViewModel
        public IEnumerable<SelectListItem>? ListUnits { get; set; }
        public IEnumerable<SelectListItem>? ListCategories { get; set; }
        public IEnumerable<SelectListItem>? ListSubCategories { get; set; }   // NEW — pre-populated on Edit only
        public List<SelectListItem> ListStorageUnitTypes { get; set; } = new();
        public StorageUnitType? RequiredStorageType { get; set; }
        public List<SelectListItem> ListRooms { get; set; } = new();
        public IEnumerable<SelectListItem>? ListLotsOfItem { get; set; }
        public IEnumerable<SelectListItem>? ListSuppliers { get; set; }

        // Create Lot
        public Lot? NewLot { get; set; } = new Lot();
    }
}
