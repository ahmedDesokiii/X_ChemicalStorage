

namespace X_ChemicalStorage.Seeds
{
    public static class DefaultRoles
    {
        public static async Task SeedAsync(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
        {
            if (!roleManager.Roles.Any())
            {
                await roleManager.CreateAsync(new ApplicationRole(Roles.Admin.ToString()));
                await roleManager.CreateAsync(new ApplicationRole(Roles.User.ToString()));
            }
        }
    }
}
