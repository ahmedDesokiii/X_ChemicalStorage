namespace X_ChemicalStorage.ViewModels
{
    public class PermissionsFormViewModel
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public string Details { get; set; }
        public List<CheckBoxViewModel> RoleCalims { get; set; }
    }
}