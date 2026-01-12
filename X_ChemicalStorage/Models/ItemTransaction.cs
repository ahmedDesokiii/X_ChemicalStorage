using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class ItemTransaction
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int? Move_Num { get; set; }
        public DateTime? Move_Date { get; set; } = DateTime.Now;
        public bool? Move_State { get; set; }
        public string? Move_Statement { get; set; } = string.Empty;

        public int? ItemId { get; set; } 
        [ForeignKey("ItemId")]
        public Item? Item { get; set; }


        public int? CurrentState { get; set; }
    }
}
