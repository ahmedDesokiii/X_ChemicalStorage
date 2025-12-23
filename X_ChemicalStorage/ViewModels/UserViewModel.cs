namespace X_ChemicalStorage.ViewModels
{
    public class UserViewModel
    {

        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; }
        
        public string PhoneNumber { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; } = string.Empty;
        
        //نشط : 2  & معلق : 1  & غير نشط : 0 
        public int? CurrentState { get; set; }
        public IEnumerable<string> Roles { get; set; }
       
    }
}
