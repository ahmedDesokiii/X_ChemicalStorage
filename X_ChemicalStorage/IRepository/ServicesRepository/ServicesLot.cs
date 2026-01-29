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
        private void DeleteLotHard(Lot lot)
        {
            // 1️⃣ Delete barcode file
            if (!string.IsNullOrEmpty(lot.BarcodeImage))
            {
                var barcodePath = Path.Combine(
                    _env.WebRootPath,
                    lot.BarcodeImage.TrimStart('/')
                );

                if (File.Exists(barcodePath))
                {
                    File.Delete(barcodePath);
                }
            }

            // 2️⃣ Hard delete from DB
            _context.Lots.Remove(lot);
        }

        #region List Lots
        public List<Lot> GetAll()
        {
            try
            {
                return _context.Lots
                    .Include(x=>x.Item)
                    .Include(x=>x.Location)
                    .Include(x=>x.Suppliers)
                    .Where(x => x.CurrentState > 0 && x.AvilableQuantity > 0)
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

        #region Create Lot (Add & Update)
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
                        item.AvilableQuantity += model.AvilableQuantity;
                        _context.Items.Update(item);
                    }
                    _context.SaveChanges();


                    // Add record to Lot Transaction
                    var trans = new LotTransaction();
                    int maxTr = _context.LotTransactions.Count() + 1;
                    trans.Move_Num = maxTr;
                    trans.Move_Statement = "Add New Lot :" + model.LotNumber;
                    trans.Move_Quantity = model.AvilableQuantity;
                    trans.Move_Date = DateTime.Now.Date;
                    trans.LotId = model.Id;
                    trans.Move_State = true;
                    trans.Total_Quantity = model.AvilableQuantity;

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
                    itemTrans.Move_Quantity = model.AvilableQuantity;
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

        #region Exchange Lot 
        // Exchange Lot
        /*
          public bool Exchange(Lot model)
          {
              try
              {
                  //Update Avilable Quantity after Transaction in Item Table
                  Item item = _context.Items.FirstOrDefault(x => x.Id == model.ItemId);
                      if (item != null)
                      {
                          item.AvilableQuantity -= model.ExchageQuantity;
                          _context.Items.Update(item);
                      }
                      _context.SaveChanges();

                      //Update Avilable Quantity after Transaction in Lot Table
                      Lot lot = _context.Lots.FirstOrDefault(x => x.Id == model.Id);
                      if (lot != null)
                      {
                          lot.AvilableQuantity -= model.ExchageQuantity;
                          _context.Lots.Update(lot);
                      }
                  _context.SaveChanges();

                  using var transaction = _context.Database.BeginTransaction();
                  // Add record to Lot Transaction
                  var trans = new LotTransaction();
                      int maxTr = _context.LotTransactions.Count() + 1;
                      trans.Move_Num = maxTr;
                      trans.Move_Statement = "Exchange Lot : " + model.LotNumber;
                      trans.Move_Quantity = model.ExchageQuantity;
                      trans.Move_Date = DateTime.Now.Date;
                      //trans.Lot = model;
                      trans.LotId = model.Id;
                      trans.Move_State = false;
                      trans.Total_Quantity = lot.AvilableQuantity;

                      trans.CreatedBy = _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User).Result.FullName;
                      trans.DeviceUsing = Environment.MachineName;
                      trans.Recipient = model.Recipient;   

                      trans.CurrentState = (int)Helper.eCurrentState.Active;
                      _context.LotTransactions.Add(trans);
                      _context.SaveChanges();

                      // Add Item Transaction Record 
                      var itemTrans = new ItemTransaction();

                      int maxTrItem = _context.ItemTransactions.Count() + 1;
                      itemTrans.Move_Num = maxTrItem;
                      itemTrans.Move_Statement = "Exchange Lot  : " + model.LotNumber;
                      itemTrans.Move_Quantity = model.ExchageQuantity;
                      itemTrans.Move_Date = DateTime.Now.Date;
                      itemTrans.Item = item;
                      itemTrans.ItemId = item.Id;
                      itemTrans.Move_State = false;
                      itemTrans.Total_Quantity = item.AvilableQuantity;

                      itemTrans.CreatedBy = _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User).Result.FullName;
                      itemTrans.DeviceUsing = Environment.MachineName;

                      itemTrans.CurrentState = (int)Helper.eCurrentState.Active;
                      _context.ItemTransactions.Add(itemTrans);
                      _context.SaveChanges();

                  // 
                  if (lot.AvilableQuantity <= 0)
                  {
                      // 1️⃣ حذف ملف الباركود
                      var barcodePath = Path.Combine(
                          _env.WebRootPath,
                          lot.BarcodeImage.TrimStart('/')
                      );

                      if (File.Exists(barcodePath))
                          File.Delete(barcodePath);

                      // 2️⃣ Hard Delete للوت
                      _context.Lots.Remove(lot);
                  }

                  _context.SaveChanges();
                  transaction.Commit();
                  return true;
              }
              catch
              {
                  return false;
              }



          }
        */
        public bool Exchange(Lot model)
        {
            using var dbTransaction = _context.Database.BeginTransaction();

            try
            {
                var lot = _context.Lots
                    .Include(l => l.Item)
                    .FirstOrDefault(x => x.Id == model.Id);

                if (lot == null)
                    return false;

                if (model.ExchageQuantity <= 0 ||
                    model.ExchageQuantity > lot.AvilableQuantity)
                    return false;

                // ================= 1️⃣ Update Quantities =================
                lot.AvilableQuantity -= model.ExchageQuantity;
                lot.Item.AvilableQuantity -= model.ExchageQuantity;

                _context.Lots.Update(lot);
                _context.Items.Update(lot.Item);

                // ================= 2️⃣ Lot Transaction =================
                var lotTrans = new LotTransaction
                {
                    Move_Num = _context.LotTransactions.Count() + 1,
                    Move_Statement = "Exchange Lot : " + lot.LotNumber,
                    Move_Quantity = model.ExchageQuantity,
                    Move_Date = DateTime.Now,
                    Move_State = false,
                    Total_Quantity = lot.AvilableQuantity,
                    LotId = lot.Id,
                    Recipient = model.Recipient,
                    CreatedBy = _userManager.GetUserAsync(
                        _httpContextAccessor.HttpContext.User).Result.FullName,
                    DeviceUsing = Environment.MachineName,
                    CurrentState = (int)Helper.eCurrentState.Active
                };

                _context.LotTransactions.Add(lotTrans);

                // ================= 3️⃣ Item Transaction =================
                var itemTrans = new ItemTransaction
                {
                    Move_Num = _context.ItemTransactions.Count() + 1,
                    Move_Statement = "Exchange Lot : " + lot.LotNumber,
                    Move_Quantity = model.ExchageQuantity,
                    Move_Date = DateTime.Now,
                    Move_State = false,
                    Total_Quantity = lot.Item.AvilableQuantity,
                    ItemId = lot.ItemId,
                    CreatedBy = lotTrans.CreatedBy,
                    DeviceUsing = lotTrans.DeviceUsing,
                    CurrentState = (int)Helper.eCurrentState.Active
                };

                _context.ItemTransactions.Add(itemTrans);

                // ================= 4️⃣ Auto Delete Lot =================
                if (lot.AvilableQuantity == 0)
                {
                    var deleteItemTrans = new ItemTransaction
                    {
                        Move_Num = _context.ItemTransactions.Count() + 2,
                        Move_Statement = "Auto Delete Lot (Quantity = 0)",
                        Move_Quantity = 0,
                        Move_Date = DateTime.Now,
                        Move_State = false,
                        Total_Quantity = lot.Item.AvilableQuantity,
                        ItemId = lot.ItemId,
                        CreatedBy = lotTrans.CreatedBy,
                        DeviceUsing = lotTrans.DeviceUsing,
                        CurrentState = (int)Helper.eCurrentState.Active
                    };

                    _context.ItemTransactions.Add(deleteItemTrans);

                    var deleteTrans = new LotTransaction
                    {
                        Move_Num = _context.LotTransactions.Count() + 2,
                        Move_Statement = "Auto Delete Lot (Quantity = 0)",
                        Move_Quantity = 0,
                        Move_Date = DateTime.Now,
                        Move_State = false,
                        Total_Quantity = 0,
                        LotId = lot.Id,
                        CreatedBy = lotTrans.CreatedBy,
                        DeviceUsing = lotTrans.DeviceUsing,
                        CurrentState = (int)Helper.eCurrentState.Active
                    };

                    _context.LotTransactions.Add(deleteTrans);

                    // Delete barcode image
                    if (!string.IsNullOrEmpty(lot.BarcodeImage))
                    {
                        var barcodePath = Path.Combine(
                            _env.WebRootPath,
                            lot.BarcodeImage.TrimStart('/'));

                        if (File.Exists(barcodePath))
                            File.Delete(barcodePath);
                    }

                    _context.Lots.Remove(lot);
                }

                _context.SaveChanges();
                dbTransaction.Commit();

                return true;
            }
            catch
            {
                dbTransaction.Rollback();
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

