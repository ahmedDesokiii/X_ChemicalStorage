namespace X_ChemicalStorage.Models
{
    public class QRCodeModel
    {
        public string? QRCodeText { get; set; }
        // Property to store the generated QR code image as a Base64 string
        public string? QRImageURL { get; set; }
    }
}
