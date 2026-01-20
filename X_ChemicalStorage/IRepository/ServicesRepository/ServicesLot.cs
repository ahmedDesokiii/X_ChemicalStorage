using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesLot : IServicesRepository<Lot> , IServicesLot
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ServicesLot(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor, ApplicationDbContext context, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _env = env;
        }

        #region List Lots
        public List<Lot> GetAll()
        {
            try
            {
                return _context.Lots
                    .Include(x=>x.Item)
                    .Include(x=>x.Location)
                    .Include(x=>x.SupplierLots)
                    .Where(x => x.CurrentState > 0 && x.TotalQuantity > 0)
                    .OrderBy(x=>x.ExpiryDate)
                    .ToList();
            }
            catch
            {
                return new List<Lot>();
            }
        }
        #endregion

        #region Find Lot by ...
        //FindCenterBy => Id | Name 
        public Lot FindBy(int Id) => _context.Lots.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
        public Lot FindBy(string Number) => _context.Lots.FirstOrDefault(x => x.LotNumber.Equals(Number) && x.CurrentState > 0);

        #endregion

        #region Save Lot (Add & Update)
        // Add | Update Lot
        public bool Save(Lot model)
        {
            try
            {
                var result = FindBy(model.Id);
                if (result == null)
                {
                    // Add record to Lot 

                   // model.LotNumber = Guid.NewGuid().ToString("N").Substring(0, 8);
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

                    var pixelData = writer.Write(model.LotNumber);

                    using var image = Image.LoadPixelData<Rgba32>(
                        pixelData.Pixels,
                        pixelData.Width,
                        pixelData.Height);

                    string folderPath = Path.Combine(_env.WebRootPath, "lotbarcodes");
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    string fileName = model.LotNumber + ".png";
                    string filePath = Path.Combine(folderPath, fileName);
                    image.Save(filePath);

                    // Add Lot Record
                    model.ReceivedDate = DateTime.Now;
                    model.BarcodeImage = "/lotbarcodes/" + fileName;
                    model.AvilableQuantity = model.TotalQuantity;
                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Lots.Add(model);
                    _context.SaveChanges();

                    //Update Avilable Quantity after Transaction in Item Table
                    Item item = _context.Items.FirstOrDefault(x => x.Id == model.ItemId);
                    if (item != null)
                    {
                        item.TotalQuantity += model.TotalQuantity;
                        item.AvilableQuantity += model.TotalQuantity;
                        _context.Items.Update(item);
                    }
                    _context.SaveChanges();


                    // Add record to Lot Transaction
                    var trans = new LotTransaction();
                    int maxTr = _context.LotTransactions.Count() + 1;
                    trans.Move_Num = maxTr;
                    trans.Move_Statement = "Add New Lot";
                    trans.Move_Quantity = model.TotalQuantity;
                    trans.Move_Date = DateTime.Now.Date;
                    trans.Lot = model;
                    trans.LotId = model.Id;
                    trans.Move_State = true;
                    trans.Total_Quantity = item.AvilableQuantity;

                    trans.CreatedBy = _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User).Result.FullName;
                    trans.DeviceUsing = Environment.MachineName;

                    trans.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.LotTransactions.Add(trans);
                    _context.SaveChanges();

                    // Add Item Transaction Record 
                    var itemTrans = new ItemTransaction();
                    
                    int maxTrItem = _context.ItemTransactions.Count() + 1;
                    itemTrans.Move_Num = maxTrItem;
                    itemTrans.Move_Statement = "Add Lot Num : "+model.LotNumber;
                    itemTrans.Move_Quantity = model.TotalQuantity;
                    itemTrans.Move_Date = DateTime.Now.Date;
                    itemTrans.Item = item;
                    itemTrans.ItemId = item.Id;
                    itemTrans.Move_State = true;
                    itemTrans.Total_Quantity = item.AvilableQuantity;

                    itemTrans.CreatedBy = _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User).Result.FullName;
                    itemTrans.DeviceUsing = Environment.MachineName;

                    itemTrans.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.ItemTransactions.Add(itemTrans);
                    _context.SaveChanges();
                }

                ///////////// Update Lot ////////////////
                else
                {
                    result.LotNumber = model.LotNumber;
                    result.TotalQuantity = model.TotalQuantity;
                    result.AvilableQuantity = model.AvilableQuantity;
                    result.ReceivedDate = model.ReceivedDate;
                    result.ManufactureDate = model.ManufactureDate;
                    result.ExpiryDate = model.ExpiryDate;
                    result.SDS = model.SDS;
                    result.LocationId = model.LocationId;
                    
                    result.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Lots.Update(result);
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

        #region Delete Lot
        public bool Delete(int Id)
        {
            try
            {
                var result = FindBy(Id);
                if (result != null)
                {
                    result.CurrentState = (int)Helper.eCurrentState.Delete;
                    _context.Lots.Update(result);
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

        #region Lot Details
        // Lot Details
        public Lot Details(int Id)
        {
            try
            {
                return _context.Lots.FirstOrDefault(x => x.Id.Equals(Id) && x.CurrentState > 0);
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region Location Details
        public Location? GetLocationDetailsOfLot(int Id)
        {
            try
            {
                var selectLot = FindBy(Id);

                return selectLot.Location;
            }
            catch
            {
                return new Location();
            }
        }
        #endregion

        #region List Transactions Of Lot
        public List<LotTransaction> GetLotTransactionsOfLot(int id)
        {
            try
            {
                return _context.LotTransactions
                                     .Include(lot => lot.Lot)
                                     .Where(x => x.CurrentState > 0 && x.LotId == id)
                                     .ToList();
            }
            catch
            {
                return new List<LotTransaction>();
            }
        }
        #endregion
    }
}

