

namespace X_ChemicalStorage.Seeds
{
    public static class DefaultUsers
    {
        //Default Parent
        public static async Task SeedUserAsync(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
        {
            //Seed Default User
            var defaultUser = new ApplicationUser
            {

                UserName = "user",
                Email = "user@test.com",
                NormalizedUserName = "user".ToUpper(),
                NormalizedEmail = "user@test.com".ToUpper(),
                EmailConfirmed = true,
                PhoneNumber = "01000000000",
                FullName = "User",
            };
            if (userManager.Users.All(u => u.Id != defaultUser.Id))
            {
                var user = await userManager.FindByEmailAsync(defaultUser.Email);
                if (user == null)
                {
                    await userManager.CreateAsync(defaultUser, "$Aa123123");
                    await userManager.AddToRoleAsync(defaultUser, Roles.User.ToString());
                }
            }
        }

        /// أدمن الموقع من الإعدادات (AdminSeed). لو الباسورد فاضي مبيعملش حاجة.
        /// بيتعمل مرة واحدة بس: لو الإيميل موجود مبيغيرش الباسورد.
        public static async Task SeedConfiguredAdminAsync(UserManager<ApplicationUser> userManager,
            Microsoft.Extensions.Configuration.IConfiguration section, Microsoft.Extensions.Logging.ILogger logger)
        {
            var email = section["Email"];
            var password = section["Password"];
            var userName = string.IsNullOrWhiteSpace(section["UserName"]) ? "admin" : section["UserName"]!;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                if (!userManager.Users.Any())
                    logger.LogWarning("No users exist and AdminSeed:Password is empty. Set it in appsettings.Local.json to create the first admin.");
                return;
            }
            if (await userManager.FindByEmailAsync(email) != null || await userManager.FindByNameAsync(userName) != null)
                return;

            var admin = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FullName = "Administrator",
            };
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                logger.LogError("Could not create site admin: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            await userManager.AddToRoleAsync(admin, Roles.Admin.ToString());
            logger.LogInformation("Site admin '{UserName}' created from AdminSeed", userName);
        }

        //Default Admin
        public static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
        {
            //Seed Default User
            var defaultUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@test.com",
                NormalizedUserName = "admin".ToUpper(),
                NormalizedEmail = "admin@test.com".ToUpper(),
                EmailConfirmed = true,
                PhoneNumber = "02000000000",
                FullName = "Admin",
            };
            if (userManager.Users.All(u => u.Id != defaultUser.Id))
            {
                var user = await userManager.FindByEmailAsync(defaultUser.Email);
                if (user == null)
                {
                    await userManager.CreateAsync(defaultUser, "$Aa123123");
                    await userManager.AddToRoleAsync(defaultUser, Roles.Admin.ToString());
                }
            }
        }
    }
}
