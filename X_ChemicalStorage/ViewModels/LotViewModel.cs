using Microsoft.AspNetCore.Mvc.Rendering;

namespace X_ChemicalStorage.ViewModels
{
    public class LotViewModel
    {
        public Lot? NewLot { get; set; } = new Lot();
        public Item? NewItem { get; set; } = new Item();

        // القديم (لأي استخدام تاني)
        public List<Lot>? LotsList { get; set; } = new();

        // الجديد للعرض
        public List<LotDisplayViewModel>? LotsDisplay { get; set; } = new();

        public List<Lot>? ExpiryLotsList { get; set; } = new();
        public List<Item>? ItemsList { get; set; } = new();
        public List<LotTransaction>? LotTransactionsList { get; set; } = new();
        public Location? LocationData { get; set; } = new Location();
        public IEnumerable<SelectListItem>? ListLocations { get; set; }


        ////// Permissions ////////////
        ///
        public bool CanCreateLot { get; set; }
        public bool CanViewExpiryLots { get; set; }
        public bool CanViewDetails { get; set; }
        public bool CanViewItemDetails { get; set; }
        public bool CanEditLot { get; set; }
        public bool CanDeleteLot { get; set; }
        public bool CanExchangeLot { get; set; }
        public bool CanPrintBarcode { get; set; }

    }

   
        public class LotDisplayViewModel
        {

            

            public Guid Id { get; set; }

            public string ItemName { get; set; }
            public string ItemCode { get; set; }

            public string LotNumber { get; set; }
            public string BarcodeImage { get; set; }

            public DateTime ExpiryDate { get; set; }
            public DateTime ManufactureDate { get; set; }
            public DateTime ReceivedDate { get; set; }

            public int AvailableQuantity { get; set; }
            public int RoomNumber { get; set; }

            public bool SDS { get; set; }

            /* ================== Calculated ================== */

            public int DaysLeft =>
                (ExpiryDate.Date - DateTime.Now.Date).Days;

            public bool IsExpired => DaysLeft <= 0;

            public bool IsNearExpiry =>
                DaysLeft > 0 && DaysLeft <= 30;

            // نفس ألوان الديسكتوب
            public string ExpiryTextClass =>
                IsExpired ? "link-danger fw-bold" :
                IsNearExpiry ? "link-warning fw-bold" :
                "link-success fw-bold";

            public string ExpiryBorderColor =>
                IsExpired ? "#f94e3b" :
                IsNearExpiry ? "#ffc107" :
                "#4caf50";

            public string SDSBadgeClass =>
                SDS ? "bg-success-subtle link-success" :
                      "bg-danger-subtle link-danger";

            public string SDSStatusText =>
                SDS ? "Valid" : "Invalid";
        }
    }

