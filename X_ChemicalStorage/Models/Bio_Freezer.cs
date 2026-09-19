using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.Models
{
    public class Bio_Freezer
    {
        [Key]
        [MaxLength(50)]
        public string Id { get; set; } = null!;

        [Required]
        public FreezerType Type { get; set; }

        public int Temperature { get; set; }
        public FreezerStatus Status { get; set; } = FreezerStatus.Online;

        [MaxLength(100)]
        public string? Barcode { get; set; }

        // ===== Configuration (Counts) =====
        public int ShelvesCount { get; set; }          // ← غيّر الاسم هنا
        public int RacksPerShelf { get; set; }
        public int DrawersPerRack { get; set; }
        public int BoxesPerDrawer { get; set; }
        public int TubesPerBox { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // ===== Navigation Properties =====
        public ICollection<Bio_Shelf> Bio_Shelves { get; set; } = new List<Bio_Shelf>();
        public ICollection<Bio_Rack> Bio_Racks { get; set; } = new List<Bio_Rack>();
    }
}

