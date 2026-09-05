using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.IRepository
{
    // Extra members beyond the generic IServicesRepository<SubCategory>,
    // same idea as IServicesItem alongside IServicesRepository<Item>.
    public interface IServicesSubCategory
    {
        List<SubCategory> GetByCategoryId(int categoryId);
    }
}
