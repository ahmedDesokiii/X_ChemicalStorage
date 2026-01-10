using Analyzer.Utilities;
using System.ComponentModel.DataAnnotations.Schema;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.Models
{
    public class Item
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? Code { get; set; } // item Code
        public string? Name { get; set; } // item Name
        public bool? SDS { get; set; } //  Safty Data Sheet 
        public int Limit { get; set; } = 1; // Minimum Limit

        //public string? BarcodeType { get; set; } // Barcode Type

        // Foreign Keys
        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }
        // Foreign Keys
        public int? UnitId { get; set; }
        [ForeignKey("UnitId")]
        public Unit? Unit { get; set; }
        // Foreign Keys
        public int? LocationId { get; set; } // Location Details
        [ForeignKey("LocationId")]
        public Location? Location { get; set; }
        // Foreign Keys
        

        public ICollection<Lot> Lots { get; set; } = new List<Lot>();

        public int? CurrentState { get; set; }
    }
}
