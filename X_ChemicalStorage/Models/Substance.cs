using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Substance
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required(ErrorMessage = " فضلا , ادخل اسم المورد بشكل صحيح")]
        [MaxLength(100, ErrorMessage = "لا يزيد اسم المورد عن 100 خانة")]
        [MinLength(3, ErrorMessage = "لا يقل اسم المورد عن 3 خانات")]
        public string? Substance_Name { get; set; } // Substance Name
        [Required(ErrorMessage = " فضلا , ادخل اسم المورد بشكل صحيح")]
        [MaxLength(100, ErrorMessage = "لا يزيد اسم المورد عن 100 خانة")]
        [MinLength(3, ErrorMessage = "لا يقل اسم المورد عن 3 خانات")]
        public string? IUPAC_Name { get; set; } // IUPAC Name
        [Required(ErrorMessage = " فضلا , ادخل اسم المورد بشكل صحيح")]
        [MaxLength(100, ErrorMessage = "لا يزيد اسم المورد عن 100 خانة")]
        [MinLength(3, ErrorMessage = "لا يقل اسم المورد عن 3 خانات")]
        public string? CAS_Number { get; set; } // Chemical Abstracts Service number
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
