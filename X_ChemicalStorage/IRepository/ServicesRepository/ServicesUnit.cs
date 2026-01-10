namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesUnit : IServicesRepository<Unit>
    {
        private readonly ApplicationDbContext _context;

        public ServicesUnit(ApplicationDbContext context)
        {
            _context = context;
        }

        #region List Units
        public List<Unit> GetAll()
        {
            try
            {
                return _context.Units.Where(x => x.CurrentState > 0).ToList();
            }
            catch
            {
                return new List<Unit>();
            }
        }
        #endregion

        #region Find Unit by ...
        //FindCenterBy => Id | Name 
        public Unit FindBy(int Id) => _context.Units.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public Unit FindBy(string Name) => _context.Units.FirstOrDefault(x => x.Name.Equals(Name.Trim()) && x.CurrentState > 0);

        #endregion

        #region Save Unit (Add & Update)
        // Add | Update Unit
        public bool Save(Unit model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Units.Add(model);
                }
                else
                {
                    result.Name = model.Name;
                    result.Details = model.Details;
                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Units.Update(result);
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

        #region Delete Unit
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result != null)
                {
                    result.CurrentState = (int)Helper.eCurrentState.Delete;
                    _context.Units.Update(result);
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

        #region Unit Details
        // Unit Details
        public Unit Details(int Id)
        {
            try
            {
                return _context.Units.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}

