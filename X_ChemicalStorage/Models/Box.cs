namespace X_ChemicalStorage.Models
{
    public class Box
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty; // "Box 1"

        public int RowsCount { get; set; }
        public int ColumnsCount { get; set; }

        public int ShelfId { get; set; }
        public Shelf Shelf { get; set; } = null!;

        public ICollection<Lot> Lots { get; set; } = new List<Lot>();
    }
}