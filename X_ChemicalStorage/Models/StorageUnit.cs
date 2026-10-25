using X_ChemicalStorage.Models.TempSamples;

namespace X_ChemicalStorage.Models
{
    public class StorageUnit
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty; // "Freezer 1", "N2 Tank 1"...

        public int RoomId { get; set; }
        public Room Room { get; set; } = null!;

        public int StorageUnitTypeId { get; set; }
        public StorageUnitType StorageUnitType { get; set; } = null!;

        public bool IsHazardous { get; set; } = false;

        public ICollection<Shelf> Shelves { get; set; } = new List<Shelf>();

        public StorageZone Zone { get; set; } = StorageZone.Inventory;

    }
}