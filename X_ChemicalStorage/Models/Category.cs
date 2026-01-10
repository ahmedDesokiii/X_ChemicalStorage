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
        // public ICollection<Substance> Substances { get; set; } = new List<Substance>();
        public ICollection<Item> Items { get; set; } = new List<Item>();


        public int? CurrentState { get; set; }
    }
}
