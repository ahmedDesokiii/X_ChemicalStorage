namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesCategory : IServicesRepository<Category>
    {
        private readonly ApplicationDbContext _context;

        public ServicesCategory(ApplicationDbContext context)
        {
            _context = context;
        }

        #region List Categories
        public List<Category> GetAll()
        {
            try
            {
                return _context.Categories.Where(x => x.CurrentState > 0).ToList();
            }
            catch
            {
                return new List<Category>();
            }
        }
        #endregion

        #region Find Category by ...
        //FindCenterBy => Id | Name 
        public Category FindBy(int Id) => _context.Categories.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public Category FindBy(string Name) => _context.Categories.FirstOrDefault(x => x.Name.Equals(Name.Trim()) && x.CurrentState > 0);

        #endregion

        #region Save Category (Add & Update)
        // Add | Update Category
        public bool Save(Category model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Categories.Add(model);
                }
                else
                {
                    result.Name = model.Name;
                    result.Details = model.Details;
                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Categories.Update(result);
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

        #region Delete Category
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result != null)
                {
                    result.CurrentState = (int)Helper.eCurrentState.Delete;
                    _context.Categories.Update(result);
                    _context.SaveChanges();
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region Category Details
        // Category Details
        public Category Details(int Id)
        {
            try
            {
                return _context.Categories.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}

