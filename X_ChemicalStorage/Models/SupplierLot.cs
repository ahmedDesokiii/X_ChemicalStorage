namespace X_ChemicalStorage.Models
{
    public class SupplierLot
    {
        
            // Foreign Key to Supplier
            public int SupplierId { get; set; }
            public Supplier Supplier { get; set; }

            // Foreign Key to Lot
            public int LotId { get; set; }
            public Lot Lot { get; set; }
    }
}
