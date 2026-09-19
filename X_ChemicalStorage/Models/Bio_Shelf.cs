using System.ComponentModel.DataAnnotations.Schema;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.Models
{
    public class Bio_Shelf
    {
        [Key]
        [MaxLength(80)]
        public string Id { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string FreezerId { get; set; } = null!;          // ← مهم

        public int ShelfIndex { get; set; }                     // ← مهم

        // Navigation
        [ForeignKey(nameof(FreezerId))]
        public Bio_Freezer Bio_Freezer { get; set; } = null!;   // ← مهم

        public ICollection<Bio_Rack> Bio_Racks { get; set; } = new List<Bio_Rack>();
    }
}

