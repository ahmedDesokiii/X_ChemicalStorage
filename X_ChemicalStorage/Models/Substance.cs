using System.ComponentModel.DataAnnotations.Schema;

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
        public string? Precautionary_Statements { get; set; } // Precautionary Statements


        // Foreign Keys
        public int? CategoryId { get; set; } // Storage Requirements
        // Foreign Keys
        public int? LocationId { get; set; } // Storage Requirements
        // Foreign Keys
        public int? SupplierId { get; set; } // Supplier Name

        public int? CurrentState { get; set; }
    }
}
