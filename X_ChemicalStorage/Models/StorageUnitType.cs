namespace X_ChemicalStorage.Models
{
    public class StorageUnitType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? TemperatureValue { get; set; }

        public ICollection<StorageUnit> StorageUnits { get; set; } = new List<StorageUnit>();
        public ICollection<Item> Items { get; set; } = new List<Item>();
    }
}