namespace X_ChemicalStorage.ViewModels
{
    public class ManufacuterCompanyViewModel
    {
        public ManufacuterCompany? NewManufacuterCompany { get; set; } = new ManufacuterCompany();
        public List<ManufacuterCompany>? ManufacuterCompaniesList { get; set; } = new List<ManufacuterCompany>();
    }
}
