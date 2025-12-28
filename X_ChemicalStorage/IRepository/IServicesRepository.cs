

namespace X_ChemicalStorage.IRepository
{
    public interface IServicesRepository<T> where T : class
    {
        List<T> GetAll();
        T FindBy(int Id);
        T FindBy(string Name);
        bool Save(T model); //add & update
        bool Delete(int Id);

    }
}
