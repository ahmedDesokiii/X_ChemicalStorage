using Microsoft.AspNetCore.Mvc.Rendering;

namespace X_ChemicalStorage.Models.ViewModels
{
    public class RoomsViewModel
    {
        public List<Room> RoomsList { get; set; } = new();
        public List<SelectListItem> ListStorageUnitTypes { get; set; } = new();

        public Room NewRoom { get; set; } = new();
        public GenerateStorageViewModel GenerateModel { get; set; } = new();

        // RoomsViewModel
        public Dictionary<int, int> HoldingOccupancy { get; set; } = new();     // UnitId → samples
        public Dictionary<int, int> HoldingBoxOccupancy { get; set; } = new();  // BoxId  → samples
        public Dictionary<int, int> LotBoxOccupancy { get; set; } = new();
    }

    // بيانات الويزارد بتاع التوليد التلقائي
    public class GenerateStorageViewModel
    {
        public string RoomName { get; set; } = string.Empty;
        public List<UnitGenerationInput> Units { get; set; } = new();
    }

    public class UnitGenerationInput
    {
        public X_ChemicalStorage.Models.TempSamples.StorageZone Zone { get; set; }
        public int StorageUnitTypeId { get; set; }
        public int UnitsCount { get; set; }          // كام وحدة من النوع ده
        public bool IsHazardous { get; set; }

        public int ShelvesPerUnit { get; set; }      // كام رف في كل وحدة
        public int BoxesPerShelf { get; set; }        // كام بوكس في كل رف
        public int RowsCount { get; set; }            // أبعاد البوكس
        public int ColumnsCount { get; set; }
    }
}