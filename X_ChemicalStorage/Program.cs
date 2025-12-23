using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString));

var Configuration = builder.Configuration;

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddDbContext<ApplicationDbContext>(ServiceLifetime.Scoped);


builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(
    options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultUI()
                .AddDefaultTokenProviders();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//Services To ReloadPage after update any process 
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});


//Email Services
//builder.Services.AddTransient<IEmailSender, EmailSender>();
//builder.Services.Configure<AuthMessageSenderOptions>(builder.Configuration);


builder.Services.AddDbContext<ApplicationDbContext>(ServiceLifetime.Transient);
builder.Services.AddSession();
builder.Services.AddControllersWithViews();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("app");
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        await X_ChemicalStorage.Seeds.DefaultRoles.SeedAsync(userManager, roleManager);
        await X_ChemicalStorage.Seeds.DefaultUsers.SeedUserAsync(userManager, roleManager);
        await X_ChemicalStorage.Seeds.DefaultUsers.SeedAdminAsync(userManager, roleManager);

        logger.LogInformation("Finished Seeding Default Data");
        logger.LogInformation("Application Starting");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "An error occurred seeding the DB");
    }
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
        