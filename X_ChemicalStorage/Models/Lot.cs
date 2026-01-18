using System.ComponentModel.DataAnnotations.Schema;
namespace X_ChemicalStorage.Models
{
    public class Lot
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? LotNumber { get; set; }
        //public double? Quantity { get; set; }
        public double? TotalQuantity { get; set; }
        public double? AvilableQuantity { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool? SDS { get; set; }
        public string? BarcodeImage { get; set; } // مسار صورة الباركود

        // Foreign Keys
        public int? LocationId { get; set; } // Location Details
        [ForeignKey("LocationId")]
        public Location? Location { get; set; }
         public int? ItemId { get; set; } 
        [ForeignKey("ItemId")]
        public Item? Item { get; set; }

        public ICollection<SupplierLot> SupplierLots { get; set; }
        public int? CurrentState { get; set; }
    }
}
