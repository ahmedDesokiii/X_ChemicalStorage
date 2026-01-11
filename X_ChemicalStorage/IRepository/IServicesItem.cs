namespace X_ChemicalStorage.IRepository
{
    public interface IServicesItem
    {
        public List<Lot> GetLotsOfItem(int id);
        public Location? GetLocationDetailsOfItem(int Id);
    }
}
