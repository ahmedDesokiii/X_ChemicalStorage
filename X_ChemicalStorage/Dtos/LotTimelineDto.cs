namespace X_ChemicalStorage.Dtos
{
    public class LotTimelineDto
    {
        public string x { get; set; } // Lot Code
        public List<DateTime> y { get; set; } // [Manufacture, Expiry]
    }
}
