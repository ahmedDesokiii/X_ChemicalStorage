namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesManufacuterCompany : IServicesRepository<ManufacuterCompany>
    {
        private readonly ApplicationDbContext _context;

        public ServicesManufacuterCompany(ApplicationDbContext context)
        {
            _context = context;
        }

        #region List ManufacuterCompanies
        public List<ManufacuterCompany> GetAll()
        {
            try
            {
                return _context.ManufacuterCompanies.Where(x => x.CurrentState > 0).ToList();
            }
            catch
            {
                return new List<ManufacuterCompany>();
            }
        }
        #endregion

        #region Find ManufacuterCompany by ...
        //FindCenterBy => Id | Name 
        public ManufacuterCompany FindBy(int Id) => _context.ManufacuterCompanies.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public ManufacuterCompany FindBy(string Name) => _context.ManufacuterCompanies.FirstOrDefault(x => x.Name.Equals(Name.Trim()) && x.CurrentState > 0);

        #endregion

        #region Save ManufacuterCompany (Add & Update)
        // Add | Update ManufacuterCompany
        public bool Save(ManufacuterCompany model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.ManufacuterCompanies.Add(model);
                }
                else
                {
                    result.Name = model.Name;
                    result.ShortCutName = model.ShortCutName;
                    result.Details = model.Details;
                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.ManufacuterCompanies.Update(result);
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

        #region Delete ManufacuterCompany
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result != null)
                {
                    result.CurrentState = (int)Helper.eCurrentState.Delete;
                    _context.ManufacuterCompanies.Update(result);
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

        #region ManufacuterCompany Details
        // ManufacuterCompany Details
        public ManufacuterCompany Details(int Id)
        {
            try
            {
                return _context.ManufacuterCompanies.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}

