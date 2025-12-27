using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Supplier
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required(ErrorMessage = " فضلا , ادخل اسم المورد بشكل صحيح")]
        [MaxLength(100, ErrorMessage = "لا يزيد اسم المورد عن 100 خانة")]
        [MinLength(3, ErrorMessage = "لا يقل اسم المورد عن 3 خانات")]
        public string? Name { get; set; }
       // [Required(ErrorMessage = "فضلا , ادخل تليفون ")]
        [Phone(ErrorMessage = "ادخل رقم تليفون مناسب")]
        public string? Phone { get; set; }
        [EmailAddress(ErrorMessage = "ادخل بريد الكتروني مناسب")]
        public string? Email { get; set; }
        public string? Adress { get; set; }
        public int? CurrentState { get; set; }
    }
}
