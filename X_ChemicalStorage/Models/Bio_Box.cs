using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Bio_Box
    {
        [Key]
        [MaxLength(150)]
        public string Id { get; set; } = null!;

        [MaxLength(120)]
        public string? DrawerId { get; set; }

        [MaxLength(100)]
        public string? RackId { get; set; }

        public int BoxIndex { get; set; }

        public int Rows { get; set; } = 10;
        public int Columns { get; set; } = 10;

        [ForeignKey(nameof(DrawerId))]
        public Bio_Drawer? Bio_Drawer { get; set; }          // ← مش Bio_Drawers

        [ForeignKey(nameof(RackId))]
        public Bio_Rack? Bio_Rack { get; set; }

        public ICollection<Bio_Tube> Bio_Tubes { get; set; } = new List<Bio_Tube>();
    }
}

