using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class LotTransaction
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int? Move_Num { get; set; }
        public DateTime? Move_Date { get; set; } = DateTime.Now;
        public bool? Move_State { get; set; }
        public string? Move_Statement { get; set; } = string.Empty;
        public double? Move_Quantity { get; set; } = 0;
        public double? Total_Quantity { get; set; } = 0;
        public string? Recipient { get; set; } = string.Empty; // جهة الاستلام

        public int? LotId { get; set; } 
        //[ForeignKey("LotId")]
        //public Lot? Lot { get; set; }

        public string? CreatedBy { get; set; } = string.Empty;
        public string? DeviceUsing { get; set; } = string.Empty;

        public int? CurrentState { get; set; }
    }
}
