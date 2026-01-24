using System.ComponentModel.DataAnnotations.Schema;
namespace X_ChemicalStorage.Models
{
    public class Lot
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? LotNumber { get; set; }
        public double? ExchageQuantity { get; set; }
        public string? Recipient { get; set; } // جهة الاستلام
        public double? TotalQuantity { get; set; }
        public double? AvilableQuantity { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? ReceivedDate { get; set; }
        
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? ManufactureDate { get; set; }
        
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
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
        public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
        //public ICollection<SupplierLot> SupplierLots { get; set; }
        public int? CurrentState { get; set; }
    }
}
