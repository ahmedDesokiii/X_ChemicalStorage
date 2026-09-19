using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Bio_Drawer
    {
        [Key]
        [MaxLength(120)]
        public string Id { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string RackId { get; set; } = null!;

        public int DrawerIndex { get; set; }

        public Bio_Rack Bio_Rack { get; set; } = null!;

        public ICollection<Bio_Box> Bio_Boxes { get; set; } = new List<Bio_Box>();
    }
}
