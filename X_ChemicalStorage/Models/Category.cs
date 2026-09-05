using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Category
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(100), MinLength(3)]
        public string? Name { get; set; }

        public string? Details { get; set; }

        // NOTE: kept exactly as-is — do not remove, Items still reference Category directly
        // for backward compatibility with existing records created before SubCategory existed.
        public ICollection<Item> Items { get; set; } = new List<Item>();

        // NEW — Category 1 : N SubCategory
        public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();

        public int? CurrentState { get; set; }
    }
}
