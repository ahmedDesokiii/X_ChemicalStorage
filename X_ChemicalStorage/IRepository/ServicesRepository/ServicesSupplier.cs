namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesSupplier : IServicesRepository<Supplier>
    {
        private readonly ApplicationDbContext _context;

        public ServicesSupplier(ApplicationDbContext context)
        {
            _context = context;
        }

        #region List Suppliers
        public List<Supplier> GetAll()
        {
            try
            {
                return _context.Suppliers.Where(x => x.CurrentState > 0).ToList();
            }
            catch
            {
                return new List<Supplier>();
            }
        }
        #endregion

        #region Find Supplier by ...
        //FindCenterBy => Id | Name 
        public Supplier FindBy(int Id) => _context.Suppliers.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public Supplier FindBy(string Name) => _context.Suppliers.FirstOrDefault(x => x.Name.Equals(Name.Trim()) && x.CurrentState > 0);

        #endregion

        #region Save Supplier (Add & Update)
        // Add | Update Supplier
        public bool Save(Supplier model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Suppliers.Add(model);
                }
                else
                {
                    result.Name = model.Name;
                    result.Phone = model.Phone;
                    result.Email = model.Email;
                    result.Adress = model.Adress;
                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Suppliers.Update(result);
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

        #region Delete Supplier
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result != null)
                {
                    result.CurrentState = (int)Helper.eCurrentState.Delete;
                    _context.Suppliers.Update(result);
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

        #region Supplier Details
        // Supplier Details
        public Supplier Details(int Id)
        {
            try
            {
                return _context.Suppliers.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}

