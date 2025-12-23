namespace X_ChemicalStorage.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        [Required, MaxLength(200), MinLength(5)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Gender { get; set; } = string.Empty;

        //password show
        public string PassUser { get; set; } = string.Empty;

        // نشط : 2  & غير نشط : 0 & معلق : 1
        public int? CurrentState { get; set; } = 0;

    }
}
