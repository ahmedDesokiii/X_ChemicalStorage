using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Category
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required(ErrorMessage = " فضلا , ادخل الفئة بشكل صحيح")]
        [MaxLength(100, ErrorMessage = "لا تزيد الفئة عن 100 خانة")]
        [MinLength(3, ErrorMessage = "لا يقل الفئة عن 3 خانات")]
        public string? Name { get; set; }
        public string? Details { get; set; }
        public ICollection<Substance> Substances { get; set; } = new List<Substance>();

        public int? CurrentState { get; set; }
    }
}
