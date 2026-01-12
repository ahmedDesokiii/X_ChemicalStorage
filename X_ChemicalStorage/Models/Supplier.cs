using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Supplier
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required, MaxLength(100), MinLength(3)]
        public string? Name { get; set; }
       // [Required(ErrorMessage = "فضلا , ادخل تليفون ")]
        [Phone]
        public string? Phone { get; set; }
        [EmailAddress]
        public string? Email { get; set; }
        public string? Adress { get; set; }

        //public ICollection<Substance> Substances { get; set; } = new List<Substance>();
        public ICollection<SupplierLot> SupplierLots { get; set; }
        public int? CurrentState { get; set; }
    }
}
