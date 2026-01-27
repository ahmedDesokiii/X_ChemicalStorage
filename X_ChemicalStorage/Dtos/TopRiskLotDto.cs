namespace X_ChemicalStorage.Dtos
{
    public class TopRiskLotDto
    {
        public int LotId { get; set; }
        public string LotCode { get; set; }
        public string MaterialName { get; set; }
        public int DaysToExpire { get; set; }
        public string RiskClass { get; set; } // high / medium / low
    }


}
