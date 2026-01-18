using X_ChemicalStorage.Dtos;

namespace X_ChemicalStorage.IRepository
{
    public interface IInventoryService
    {
        Task<List<LotDto>> GetLotsByBarcodeAsync(string barcode);
    }
}
