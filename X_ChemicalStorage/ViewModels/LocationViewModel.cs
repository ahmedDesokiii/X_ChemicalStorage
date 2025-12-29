namespace X_ChemicalStorage.ViewModels
{
    public class LocationViewModel
    {
        public Location? NewLocation { get; set; } = new Location();
        public List<Location>? LocationsList { get; set; } = new List<Location>();
    }
}
