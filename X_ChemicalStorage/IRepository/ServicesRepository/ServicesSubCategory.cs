using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesSubCategory : IServicesRepository<SubCategory>, IServicesSubCategory
    {
        private readonly ApplicationDbContext _context;

        public ServicesSubCategory(ApplicationDbContext context)
        {
            _context = context;
        }

        #region List SubCategories
        public List<SubCategory> GetAll()
        {
            try
            {
                return _context.SubCategories
                                .Where(x => x.CurrentState > 0)
                                .Include(x => x.Category)
                                .ToList();
            }
            catch
            {
                return new List<SubCategory>();
            }
        }
        #endregion

        #region SubCategories filtered by Category (for the cascading dropdown)
        public List<SubCategory> GetByCategoryId(int categoryId)
        {
            try
            {
                return _context.SubCategories
                                .AsNoTracking()
                                .Where(x => x.CategoryId == categoryId && x.CurrentState > 0)
                                .OrderBy(x => x.Name)
                                .ToList();
            }
            catch
            {
                return new List<SubCategory>();
            }
        }
        #endregion

        #region Find SubCategory by ...
        public SubCategory FindBy(int Id) =>
            _context.SubCategories.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);

        public SubCategory FindBy(string Name) =>
            _context.SubCategories.FirstOrDefault(x => x.Name.Equals(Name.Trim()) && x.CurrentState > 0);
        #endregion

        #region Save SubCategory (Add & Update)
        public bool Save(SubCategory model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.SubCategories.Add(model);
                }
                else
                {
                    // Prevent moving a SubCategory to a different Category silently changing
                    // Items that already reference it — Category reassignment isn't supported
                    // from the edit form; only Name/Details are editable here.
                    result.Name = model.Name;
                    result.Details = model.Details;
                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.SubCategories.Update(result);
                }
                _context.SaveChanges();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region Delete SubCategory (soft-delete, blocked if Items still reference it)
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result == null)
                    return false;

                bool hasItems = _context.Items.Any(i => i.SubCategoryId == Id);
                if (hasItems)
                    return false; // caller shows "cannot delete, items depend on it"

                result.CurrentState = (int)Helper.eCurrentState.Delete;
                _context.SubCategories.Update(result);
                _context.SaveChanges();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region SubCategory Details
        public SubCategory Details(int Id)
        {
            try
            {
                return _context.SubCategories
                                .Include(x => x.Category)
                                .FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}
