namespace X_ChemicalStorage.IRepository
{
    public interface IServicesLot
    {
        //public List<Lot> GetLotsOfItem(int id);
        public bool Exchange(Lot model);
        public Location? GetLocationDetailsOfLot(int Id);
        public List<LotTransaction> GetLotTransactionsOfLot(int id);

    }
}
