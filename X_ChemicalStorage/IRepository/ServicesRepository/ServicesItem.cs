namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesItem : IServicesRepository<Item> , IServicesItem
    {
        private readonly ApplicationDbContext _context;

        public ServicesItem(ApplicationDbContext context)
        {
            _context = context;
        }

        #region List Items
        public List<Item> GetAll()
        {
            try
            {
                return _context.Items
                                     .Include(item=>item.Location)
                                     .Include(item=>item.Category)
                                     .Include(item=>item.Unit)

                                     .Where(x => x.CurrentState > 0)
                                     .ToList();
            }
            catch
            {
                return new List<Item>();
            }
        }
        #endregion

        #region Find Item by ...
        //FindCenterBy => Id | Name 
        public Item FindBy(int Id) => _context.Items.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public Item FindBy(string Name) => _context.Items.FirstOrDefault(x => x.Name.Equals(Name.Trim()) && x.CurrentState > 0);
        
        #endregion

        #region Save Item (Add & Update)
        // Add | Update Item
        public bool Save(Item model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    model.AvilableQuantity = model.TotalQuantity;
                    _context.Items.Add(model);
                }
                else
                {
                    result.Code = model.Code;
                    result.Name = model.Name;
                    result.Limit = model.Limit;
                    //result.TotalQuantity = model.TotalQuantity;
                    //result.AvilableQuantity = model.TotalQuantity;
                    result.SDS = model.SDS;
                    result.CategoryId = model.CategoryId;
                    result.UnitId = model.UnitId;
                    result.LocationId = model.LocationId;

                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Items.Update(result);
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

        #region Delete Item
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result != null)
                {
                    result.CurrentState = (int)Helper.eCurrentState.Delete;
                    _context.Items.Update(result);
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

        #region Item Details
        // Item Details
        public Item Details(int Id)
        {
            try
            {
                return _context.Items.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region Location Details
        public Location? GetLocationDetailsOfItem(int Id)
        {
            try
            {
                return _context.Locations.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return new Location();
            }
        }
        #endregion

        #region List Lots Of Item
        public List<Lot> GetLotsOfItem(int id)
        {
            try
            {
                return _context.Lots
                                     .Include(item => item.Location)
                                     .Include(item => item.Item)

                                     .Where(x => x.CurrentState > 0 && x.ItemId == id)
                                     .ToList();
            }
            catch
            {
                return new List<Lot>();
            }
        }
        #endregion
        #region List Lots Of Item
        public List<ItemTransaction> GetItemTransactionsOfItem(int id)
        {
            try
            {
                return _context.ItemTransactions
                                     .Include(item => item.Item)

                                     .Where(x => x.CurrentState > 0 && x.ItemId == id)
                                     .ToList();
            }
            catch
            {
                return new List<ItemTransaction>();
            }
        }
        #endregion
    }
}

