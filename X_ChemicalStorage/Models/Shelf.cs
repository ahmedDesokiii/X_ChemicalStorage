namespace X_ChemicalStorage.Models
{
    public class Shelf
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty; // "Shelf 1"

        public int StorageUnitId { get; set; }
        public StorageUnit StorageUnit { get; set; } = null!;

        public ICollection<Box> Boxes { get; set; } = new List<Box>();
    }
}