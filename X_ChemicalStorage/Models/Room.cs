namespace X_ChemicalStorage.Models
{
    public class Room
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<StorageUnit> StorageUnits { get; set; } = new List<StorageUnit>();
    }
}