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

        public string? RoomType { get; set; }
        public int? RoomNum { get; set; }
        public int? ShelfNum { get; set; }
        public int? RackNum { get; set; }
        public int? BoxNum { get; set; }

        public int? CurrentState { get; set; }
    }
}
