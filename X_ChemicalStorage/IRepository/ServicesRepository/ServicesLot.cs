using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace X_ChemicalStorage.IRepository.ServicesRepository
{
    public class ServicesLot : IServicesRepository<Lot>
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ServicesLot(ApplicationDbContext context, IWebHostEnvironment env)
        {
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
        public Lot FindBy(string Number) => _context.Lots.FirstOrDefault(x => x.LotNumber.Equals(Number.Trim()) && x.CurrentState > 0);

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
                    model.LotNumber = Guid.NewGuid().ToString("N").Substring(0, 8);
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

                    string folderPath = Path.Combine(_env.WebRootPath, "barcodes");
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    string fileName = model.LotNumber + ".png";
                    string filePath = Path.Combine(folderPath, fileName);
                    image.Save(filePath);

                    model.BarcodeImage = "/lotbarcodes/" + fileName;

                    model.CurrentState = (int)Helper.eCurrentState.Active;
                    _context.Lots.Add(model);
                }
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
    }
}

