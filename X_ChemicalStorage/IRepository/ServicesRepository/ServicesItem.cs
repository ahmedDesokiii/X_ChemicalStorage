using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Drawing.Printing;
using X_ChemicalStorage.Models;
using ZXing;
using ZXing.Common;
using static X_ChemicalStorage.Constants.Permissions;

namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesItem : IServicesRepository<Item> , IServicesItem
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
                                     .Include(item=>item.Location)
                                     .Include(item=>item.Lots)
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
                    //model.Code = Guid.NewGuid().ToString("N").Substring(0, 8);
                    //Generate Barcode !
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
                    model.AvilableQuantity = model.TotalQuantity;
                    _context.Items.Add(model);
                    _context.SaveChanges();

                    // Add record to Item Transaction
                    var trans = new ItemTransaction();
                    int maxTr = _context.ItemTransactions.Count() + 1;
                    trans.Move_Num = maxTr;
                    trans.Move_Statement = "Add New Item";
                    trans.Move_Quantity = model.TotalQuantity;
                    trans.Move_Date = DateTime.Now.Date;
                    trans.Item = model;
                    trans.ItemId = model.Id;
                    trans.Move_State = true;
                    trans.Total_Quantity = model.TotalQuantity;
                    
                    trans.CreatedBy =  _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User).Result.FullName;
                    trans.DeviceUsing = Environment.MachineName;

                    trans.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.ItemTransactions.Add(trans);

                }
                else
                {
                    //result.Code = model.Code;
                    //result.BarcodeImage = model.BarcodeImage;
                    //result.TotalQuantity = model.TotalQuantity;
                    //result.AvilableQuantity = model.TotalQuantity;
                    result.Name = model.Name;
                    result.Limit = model.Limit;
                    result.SDS = model.SDS;
                    result.StorageCondition = model.StorageCondition;
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
                var selectItem = FindBy(Id);
                
                return selectItem.Location;
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
                    .Include(x => x.Item)
                    .Include(x => x.Location)
                    .Include(x => x.SupplierLots)
                    .Where(x => x.CurrentState > 0 && x.TotalQuantity > 0 && x.ItemId == id && x.ExpiryDate >= DateTime.Now.Date)
                    .OrderBy(x => x.ExpiryDate).ThenBy(x=>x.ItemId).ThenBy(x=>x.LotNumber)
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

