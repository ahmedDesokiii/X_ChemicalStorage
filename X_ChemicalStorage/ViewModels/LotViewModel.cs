namespace X_ChemicalStorage.ViewModels
{
    public class LotViewModel
    {
        public Lot? NewLot { get; set; } = new Lot();
        public List<Lot>? LotsList { get; set; } = new List<Lot>();
        public List<Lot>? ExpiryLotsList { get; set; } = new List<Lot>();
        public List<Item>? ItemsList { get; set; } = new List<Item>();
        public List<LotTransaction>? LotTransactionsList { get; set; } = new List<LotTransaction>();
        public Location? LocationData { get; set; } = new Location();
    }
}
