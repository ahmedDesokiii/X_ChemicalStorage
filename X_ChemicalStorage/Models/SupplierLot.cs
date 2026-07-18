namespace X_ChemicalStorage.Models
{
    public class SupplierLot
    {
        public int Id { get; set; }   // ✅ ضيف ده

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        public int LotId { get; set; }
        public Lot Lot { get; set; }
    }
}
