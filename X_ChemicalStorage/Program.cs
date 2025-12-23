using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using X_ChemicalStorage.Data;

var builder = WebApplication.CreateBuilder(args);

//Using Local pc
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
          throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString));



builder.Services.AddControllersWithViews(); // Adds MVC services\
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30);
    serverOptions.Limits.MaxRequestHeadersTotalSize = 32768;
    serverOptions.Limits.MaxRequestBodySize = 104857600;
});
builder.Services.AddHttpClient("TwilioClient", client =>
{
    client.BaseAddress = new Uri("https://api.twilio.com/");
    client.Timeout = TimeSpan.FromMinutes(30);
});

// Add services to the container.
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

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

//// Authorization Policy Provider and Handler
//builder.Services.ConfigureApplicationCookie(options =>
//{
//    //options.LoginPath = "/Account/Login"; // Or any custom path
//    options.AccessDeniedPath = "/"; // Or any custom path

//});


builder.Services.AddLocalization(options => options.ResourcesPath = "Resources"); // Specify resource file location
builder.Services.AddMvc()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(); // For localizing validation messages

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
            new CultureInfo("en-US"),
            new CultureInfo("ar") // Add Arabic culture
        };

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider()); // Use cookie to store culture
});

#region Scoped Services

#endregion

var app = builder.Build();
app.UseSession();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("app");
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        //await X_ChemicalStorage.Seeds.DefaultRoles.SeedAsync(userManager, roleManager);
        //await ERPWeb_v02.Seeds.DefaultUsers.SeedUserAsync(userManager, roleManager);
        //await ERPWeb_v02.Seeds.DefaultUsers.SeedAdminAsync(userManager, roleManager);

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
app.UseAuthentication();
app.UseAuthorization();
//app.MapHub<ProgressHub>("/progressHub");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
