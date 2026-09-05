using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.Models.ViewModels;
using ZXing;
using ZXing.Common;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesItem : IServicesRepository<Item>, IServicesItem
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ServicesItem(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor, ApplicationDbContext context, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _env = env;
        }

        #region List Items
        public List<Item> GetAll()
        {
            try
            {
                return _context.Items
                                     .Include(item => item.StorageUnitType)
                                     .Include(item => item.Lots)
                                     .Include(item => item.Category)
                                     .Include(item => item.SubCategory)   // NEW
                                     .Include(item => item.Unit)
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
        public Item FindBy(int Id) => _context.Items.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public Item FindBy(string Name) => _context.Items.FirstOrDefault(x => x.Name.Equals(Name.Trim()) && x.CurrentState > 0);
        #endregion

        #region Save Item (Add & Update)
        public bool Save(Item model)
        {
            try
            {
                var result = FindBy(model.Id);

                if (result == null)
                {
                    var writer = new BarcodeWriterPixelData
                    {
                        Format = BarcodeFormat.CODE_128,
                        Options = new EncodingOptions
                        {
                            Width = 300,
                            Height = 120,
                            Margin = 2
                        }
                    };

                    var pixelData = writer.Write(model.Code);

                    using var image = Image.LoadPixelData<Rgba32>(
                        pixelData.Pixels,
                        pixelData.Width,
                        pixelData.Height);

                    string folderPath = Path.Combine(_env.WebRootPath, "barcodes");
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    string fileName = model.Code + ".png";
                    string filePath = Path.Combine(folderPath, fileName);
                    image.Save(filePath);

                    model.BarcodeImage = "/barcodes/" + fileName;
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Items.Add(model);
                    _context.SaveChanges();

                    var trans = new ItemTransaction();
                    int maxTr = _context.ItemTransactions.Count() + 1;
                    trans.Move_Num = maxTr;
                    trans.Move_Statement = "Add New Item";
                    trans.Move_Quantity = model.AvilableQuantity;
                    trans.Move_Date = DateTime.Now.Date;
                    trans.Item = model;
                    trans.ItemId = model.Id;
                    trans.Move_State = true;
                    trans.Total_Quantity = model.AvilableQuantity;

                    trans.CreatedBy = _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User).Result.FullName;
                    trans.DeviceUsing = Environment.MachineName;

                    trans.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.ItemTransactions.Add(trans);
                }
                else
                {
                    result.Name = model.Name;
                    result.Limit = model.Limit;
                    result.SDS = model.SDS;
                    result.StorageUnitTypeId = model.StorageUnitTypeId;
                    result.CategoryId = model.CategoryId;
                    result.SubCategoryId = model.SubCategoryId;   // NEW — was missing, Edit was silently dropping this
                    result.UnitId = model.UnitId;

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

        #region Storage Condition Details (بديل GetLocationDetailsOfItem القديمة)
        public StorageUnitType? GetRequiredStorageConditionOfItem(int Id)
        {
            try
            {
                return _context.Items
                    .Include(i => i.StorageUnitType)
                    .FirstOrDefault(x => x.Id == Id)?.StorageUnitType;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region List Lots Of Item
        public List<Lot> GetLotsOfItem(int id)
        {
            try
            {
                return _context.Lots
                    .Include(x => x.Item)
                    .Include(x => x.Box)
                        .ThenInclude(b => b.Shelf)
                            .ThenInclude(s => s.StorageUnit)
                                .ThenInclude(u => u.Room)
                    .Include(x => x.Suppliers)
                    .Where(x => x.CurrentState > 0 && x.AvilableQuantity > 0 && x.ItemId == id && x.ExpiryDate >= DateTime.Now.Date)
                    .OrderBy(x => x.ExpiryDate).ThenBy(x => x.ItemId).ThenBy(x => x.LotNumber)
                    .ToList();
            }
            catch
            {
                return new List<Lot>();
            }
        }
        #endregion

        #region List Transactions Of Item
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
