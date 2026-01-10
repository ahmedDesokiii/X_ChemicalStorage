namespace X_ChemicalStorage.Models
{
    public class ApplicationRole : IdentityRole<int>
    {
        public ApplicationRole(string roleName) : base(roleName)
        {
        }
        public ApplicationRole() { }

        public string Details { get; set; } = string.Empty;
        // نشط : 2  & غير نشط : 0 & معلق : 1
       // public int? CurrentState { get; set; } = 0;
    }
}
