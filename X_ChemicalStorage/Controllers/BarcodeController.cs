using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Drawing;

namespace X_ChemicalStorage.Controllers
{
    public class BarcodeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // ... existing code

        public IActionResult CreateQRCode()
        {
            return View(new QRCodeModel());
        }

        [HttpPost]
        public IActionResult CreateQRCode(QRCodeModel model)
        {
            // Generate QR code
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(model.QRCodeText ?? "Default Text", QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            {
                // GetGraphic returns a Bitmap image
                using (Bitmap qrCodeAsBitmap = qrCode.GetGraphic(20))
                {
                    // Convert Bitmap to byte array, then to Base64 string
                    model.QRImageURL = "data:image/png;base64," + Convert.ToBase64String(BitmapToByteArray(qrCodeAsBitmap));
                }
            }
            return View(model);
        }

        // Helper method to convert Bitmap to Byte Array
        private static byte[] BitmapToByteArray(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }
}
