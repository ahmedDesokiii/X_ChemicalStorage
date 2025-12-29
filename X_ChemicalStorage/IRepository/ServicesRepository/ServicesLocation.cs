namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesLocation : IServicesRepository<Location>
    {
        private readonly ApplicationDbContext _context;

        public ServicesLocation(ApplicationDbContext context)
        {
            _context = context;
        }

        #region List Locations
        public List<Location> GetAll()
        {
            try
            {
                return _context.Locations.Where(x => x.CurrentState > 0).ToList();
            }
            catch
            {
                return new List<Location>();
            }
        }
        #endregion

        #region Find Location by ...
        //FindCenterBy => Id | Name 
        public Location FindBy(int Id) => _context.Locations.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public Location FindBy(string Name) => _context.Locations.FirstOrDefault(x => x.LocationName.Equals(Name.Trim()) && x.CurrentState > 0);

        #endregion

        #region Save Location (Add & Update)
        // Add | Update Location
        public bool Save(Location model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Locations.Add(model);
                }
                else
                {
                    result.LocationName = model.LocationName;
                    result.LocationDetails = model.LocationDetails;
                    result.RoomNum = model.RoomNum;
                    result.RoomType = model.RoomType;
                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Locations.Update(result);
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

        #region Delete Location
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result != null)
                {
                    result.CurrentState = (int)Helper.eCurrentState.Delete;
                    _context.Locations.Update(result);
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

        #region Location Details
        // Location Details
        public Location Details(int Id)
        {
            try
            {
                return _context.Locations.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}

