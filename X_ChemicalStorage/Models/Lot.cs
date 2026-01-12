using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Lot
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int? LotNumber { get; set; }

        public double? TotalQuantity { get; set; }
        public double? AvilableQuantity { get; set; }
        
        public DateTime? ReceivedDate { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public string? BarcodeType { get; set; }
        public string? BarcodeValue { get; set; }
        public string? SDS { get; set; }

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
