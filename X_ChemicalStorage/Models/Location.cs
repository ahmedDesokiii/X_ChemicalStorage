using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Location
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required(ErrorMessage = " فضلا , ادخل اسم المكان بشكل صحيح")]
        [MaxLength(100, ErrorMessage = "لا تزيد اسم المكان عن 100 خانة")]
        [MinLength(3, ErrorMessage = "لا يقل اسم المكان عن 3 خانات")]
        public string? LocationName { get; set; }
        public string? LocationDetails { get; set; }

        public string? RoomType { get; set; } = String.Empty;
        public int? RoomNum { get; set; } = 0;
        public int? CaseNum { get; set; } = 0;
        public int? ShelfNum { get; set; } = 0;
        public int? RackNum { get; set; } = 0;
        public int? BoxNum { get; set; } = 0;
        public int? TubeNum { get; set; } = 0;

        public int? CurrentState { get; set; }

        public ICollection<Substance> Substances { get; set; } = new List<Substance>();
        /*
        public string? Building { get; set; } // Building Name
        public int? Room_Number { get; set; } // Room Number
        public int? Specific_Location { get; set; } // Specific Location Details within the Room (e.g., cabinet, shelf number , Freezer #1, Flammable Cabinet etc.)
        public string? Storage_Type { get; set; } // Storage Type (e.g., Cabinet, Shelf, Freezer, Refrigerator) 
        public string? Temperature_Requirement { get; set; } //Temperature Requirement (e.g., Room Temperature, Refrigerated, Frozen) 
    */
    }
}
