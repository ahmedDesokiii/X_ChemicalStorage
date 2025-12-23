namespace X_ChemicalStorage.ViewModels
{
    public class ProfileFormViewModel
    {
        public string Id { get; set; }
      
        [Required, MaxLength(200), MinLength(5)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Gender { get; set; } = string.Empty;

        [Phone]
        [Required]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "User Name")]
        public string UserName { get; set; }
        public string PassUser { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }


        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }



    }
}
