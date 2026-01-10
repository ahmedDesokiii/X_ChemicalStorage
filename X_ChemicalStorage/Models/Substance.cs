using Analyzer.Utilities;
using System.ComponentModel.DataAnnotations.Schema;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.Models
{
    public class Substance
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? Substance_Name { get; set; } // Substance Name
        public string? IUPAC_Name { get; set; } // IUPAC Name
        public string? CAS_Number { get; set; } // Chemical Abstracts Service number
        public string? Synonym { get; set; }  

        public string? Chemical_Formula { get; set; } // Chemical Formula
        public string? Physical_State { get; set; } // Physical State (e.g., solid, liquid, gas)
        public string? GHS_Classification { get; set; } // GHS Classification
        public string? Hazard_Statements { get; set; } // Hazard Statements
        public double? Size { get; set; } // Hazard Statements
        public double? Concentration { get; set; } // concentration
        public string? VendorName { get; set; } // Vendor Name

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
        public int? SupplierId { get; set; } // Supplier Name
        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }
        // Foreign Keys
        public int? ManufacturerId { get; set; } // Manufacturer Id
        [ForeignKey("ManufacturerId")]
        public ManufacuterCompany? Manufacturer { get; set; }

        public ICollection<Lot> Lots { get; set; } = new List<Lot>();

        public int? CurrentState { get; set; }
    }
}
