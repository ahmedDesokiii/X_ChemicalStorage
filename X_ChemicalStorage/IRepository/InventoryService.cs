using X_ChemicalStorage.Dtos;

namespace X_ChemicalStorage.IRepository
{
    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<LotDto>> GetLotsByBarcodeAsync(string barcode)
        {
            var lots = await _context.Items
                .Where(i => i.Code == barcode)
                .SelectMany(i => i.Lots)
                .Where(l => l.AvilableQuantity > 0)
                .OrderBy(l => l.ExpiryDate)
                .Select(l => new LotDto
                {
                    Id = l.Id,
                    AvailableQty = l.AvilableQuantity,
                    DisplayText =
                        $"{l.LotNumber} | Qty: {l.AvilableQuantity} | Exp: {l.ExpiryDate:yyyy-MM-dd}"
                })
                .ToListAsync();

            return lots;
        }
    }
}

