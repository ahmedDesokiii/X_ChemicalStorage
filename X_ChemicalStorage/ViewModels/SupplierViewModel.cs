namespace X_ChemicalStorage.ViewModels
{
    public class SupplierViewModel
    {
        public Supplier? NewSupplier { get; set; } = new Supplier();
        public List<Supplier>? SuppliersList { get; set; } = new List<Supplier>();
    }
}
