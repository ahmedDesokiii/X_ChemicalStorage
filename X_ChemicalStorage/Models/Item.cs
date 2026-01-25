using Analyzer.Utilities;
using System.ComponentModel.DataAnnotations.Schema;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.Models
{
    public class Item
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public string? Code { get; set; } // item Code // الكود الذي سيولد منه الباركود
        [Required]
        public string? Name { get; set; } // item Name
        [Required]
        public bool? SDS { get; set; } //  Safty Data Sheet 
        [Required]
        public int Limit { get; set; } = 1; // Minimum Limit
        
        public double? TotalQuantity { get; set; } = 0;
        public double? AvilableQuantity { get; set; } = 0;
        [Required]
        public string? StorageCondition { get; set; } // Storage Condition Details 
        [Required]
        public string? BarcodeImage { get; set; } // مسار صورة الباركود

        // Foreign Keys
        [Required]
        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }
        // Foreign Keys
        [Required]
        public int? UnitId { get; set; }
        [ForeignKey("UnitId")]
        public Unit? Unit { get; set; }
        // Foreign Keys
        [Required]
        public int? LocationId { get; set; } // Location Details
        [ForeignKey("LocationId")]
        public Location? Location { get; set; }
        // Foreign Keys
        

        public ICollection<Lot> Lots { get; set; } = new List<Lot>();
        public ICollection<ItemTransaction> ItemTransactions { get; set; } = new List<ItemTransaction>();

        public int? CurrentState { get; set; }
    }
}
