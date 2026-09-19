namespace X_ChemicalStorage.Models
{
    public class Bio_Rack
    {
        [Key]
        [MaxLength(100)]
        public string Id { get; set; } = null!;          // e.g. "FZ80-01-S01-R01"

        [Required]
        [MaxLength(50)]
        public string FreezerId { get; set; } = null!;

        [MaxLength(80)]
        public string? ShelfId { get; set; }             // null for -150 freezers

        public int ShelfIndex { get; set; }              // 0 for -150
        public int RackIndex { get; set; }

        public Bio_Freezer Bio_Freezer { get; set; } = null!;
        public Bio_Shelf? Bio_Shelf { get; set; }
        public ICollection<Bio_Drawer> Bio_Drawers { get; set; } = new List<Bio_Drawer>();
        public ICollection<Bio_Box> Bio_Boxes { get; set; } = new List<Bio_Box>(); // for -150
    }
}
