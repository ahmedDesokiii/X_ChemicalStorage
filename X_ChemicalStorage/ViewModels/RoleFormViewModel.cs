namespace X_ChemicalStorage.ViewModels
{
    public class RoleFormViewModel
    {
        public String RoleId { get; set; }
        [Required, StringLength(256)]
        public string Name { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

    }
}