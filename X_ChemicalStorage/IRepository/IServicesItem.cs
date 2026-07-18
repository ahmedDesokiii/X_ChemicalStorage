namespace X_ChemicalStorage.IRepository
{
    public interface IServicesItem
    {
        public List<Lot> GetLotsOfItem(int id);
       
        public List<ItemTransaction> GetItemTransactionsOfItem(int id);
        StorageUnitType? GetRequiredStorageConditionOfItem(int Id);
    }
}
