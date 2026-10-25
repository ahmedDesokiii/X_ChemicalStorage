using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.BioBank.Services;


var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddHangfire(config =>
//{
//    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
//          .UseSimpleAssemblyNameTypeSerializer()
//          .UseRecommendedSerializerSettings()
//          .UseSqlServerStorage(
//              builder.Configuration.GetConnectionString("DefaultConnection"));
//});

//builder.Services.AddHangfireServer();




// Add services to the container.
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
//    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
//builder.Services.AddDbContext<ApplicationDbContext>(
//    options => options.UseSqlServer(connectionString));

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var selectedConn = httpContextAccessor.HttpContext?.Session?.GetString("SelectedConnection") ?? "DefaultConnection";
    var connectionString = configuration.GetConnectionString(selectedConn);

    options.UseSqlServer(connectionString);
    options.UseSqlServer(connectionString)
       .AddInterceptors(new ReadingImmutabilityInterceptor());
});


builder.Services.AddControllersWithViews(); // Adds MVC services\
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddDbContext<ApplicationDbContext>(ServiceLifetime.Scoped);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

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

//builder.Services.AddDbContext<ApplicationDbContext>(ServiceLifetime.Transient);
//builder.Services.AddSession();
//builder.Services.AddControllersWithViews();

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

// ===== في Program.cs (جنب باقي التسجيلات) =====
builder.Services.AddScoped<IFreezerAlarmEngine, FreezerAlarmEngine>();

builder.Services.AddHostedService<FreezerWatchdogService>();

builder.Services.AddScoped<IServicesRepository<SubCategory>, ServicesSubCategory>();
builder.Services.AddScoped<IServicesSubCategory, ServicesSubCategory>();
builder.Services.AddScoped<IBioBankService, BioBankService>();
builder.Services.AddScoped<ISampleService, SampleService>();
builder.Services.AddScoped<IServicesRepository<Supplier>, ServicesSupplier>();
builder.Services.AddScoped<IServicesRepository<Category>, ServicesCategory>();
builder.Services.AddScoped<IServicesRepository<Unit>, ServicesUnit>();
builder.Services.AddScoped<IServicesRepository<Item>, ServicesItem>();
builder.Services.AddScoped<IServicesItem, ServicesItem>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IServicesLot, ServicesLot>();
builder.Services.AddScoped<ITempSampleService, TempSampleService>();

builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IServicesRepository<Lot>, ServicesLot>();
builder.Services.AddScoped<IServicesRepository<ManufacuterCompany>, ServicesManufacuterCompany>();
// جنب باقي الـ builder.Services.AddScoped<...>
builder.Services.AddScoped<IBioAttachmentService, BioAttachmentService>();
builder.Services.AddScoped<IFreezerIoTService, FreezerIoTService>();

//builder.Services.AddScoped<LotCleanupJob>();

builder.Services.AddScoped<IFreezerAlarmEngine, FreezerAlarmEngine>();
builder.Services.AddHostedService<FreezerWatchdogService>();
builder.Services.AddSingleton<IAlarmNotifier, OutboxAlarmNotifier>();

builder.Services.AddScoped<IIoTDashboardService, IoTDashboardService>();

builder.Services.Configure<X_ChemicalStorage.BioBank.Options.BackupOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.AddScoped<IBackupExecutor, SqlServerBackupExecutor>();
builder.Services.AddScoped<SystemNoticeService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddHostedService<DailyBackupWorker>();

#endregion




// عشان الـ multipart form data يقبل لحد 100MB
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 105_000_000;
});

// إعدادات MQTT
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));

// الـ Background Service
builder.Services.AddHostedService<FreezerMqttBackgroundService>();
builder.Services.Configure<X_ChemicalStorage.BioBank.Options.NotificationOptions>(
    builder.Configuration.GetSection("Notifications"));


builder.Services.AddSingleton<IoTHealth>();
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.AddHttpClient();                       
builder.Services.AddHostedService<HeartbeatService>();

builder.Services.AddSingleton<INotificationChannel, SmsHttpChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailSmtpChannel>();
builder.Services.AddSingleton<INotificationChannel, TelegramChannel>();
builder.Services.AddSingleton<IAlarmNotifier, OutboxAlarmNotifier>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddHostedService<AlarmNotificationWorker>();

var app = builder.Build();
app.UseSession();


using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("app");
    try
    {
        // 🔥 Auto Apply Migrations
        //var db = services.GetRequiredService<ApplicationDbContext>();
        //db.Database.Migrate();

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
        // log error
        Console.WriteLine(ex.Message);
        logger.LogWarning(ex, "An error occurred seeding the DB");
        throw;
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    //db.Database.Migrate();
}
//app.Urls.Add("http://localhost:5000");

//var url = "http://localhost:5000";

//System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
//{
//    FileName = url,
//    UseShellExecute = true
//});

app.Run();