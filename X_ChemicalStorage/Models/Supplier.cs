using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Supplier
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required, MaxLength(100), MinLength(3)]
        public string? Name { get; set; }
        [Phone]
        public string? Phone { get; set; }
        [EmailAddress]
        public string? Email { get; set; }
        public string? Adress { get; set; }
        //public ICollection<SupplierLot> SupplierLots { get; set; }
        public int? LotId { get; set; }
        [ForeignKey("LotId")]
        public Lot? Lot { get; set; }

        public int? CurrentState { get; set; }
    }
}
