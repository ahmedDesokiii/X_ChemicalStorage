using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class SubCategory
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(100), MinLength(3)]
        public string? Name { get; set; }

        public string? Details { get; set; }

        // Parent Category (mandatory — a SubCategory always belongs to one Category)
        [Required]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        // Items that belong to this SubCategory
        public ICollection<Item> Items { get; set; } = new List<Item>();

        // Same soft-delete convention used across Category/Unit/Location/etc.
        public int? CurrentState { get; set; }
    }
}
