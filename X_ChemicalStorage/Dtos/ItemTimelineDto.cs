namespace X_ChemicalStorage.Dtos
{
    public class ItemTimelineDto
    {
        public string ItemName { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Risk { get; set; } // high / medium / low
        public int TotalQty { get; set; }
    }

}
