namespace X_ChemicalStorage.ViewModels
{
    public class LotViewModel
    {
        public Lot? NewLot { get; set; } = new Lot();
        public List<Lot>? LotsList { get; set; } = new List<Lot>();
    }
}
