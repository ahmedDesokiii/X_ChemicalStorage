using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.BioBank.Services;


// ===== Hosting: يشتغل عادي من Visual Studio، أو كـ Windows Service على جهاز المعمل =====
var isWindowsService = Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // الـ Service بيبدأ من C:\Windows\System32، فلازم نثبت المسار على فولدر البرنامج
    ContentRootPath = isWindowsService ? AppContext.BaseDirectory : default
});

// الأسرار الخاصة بكل موقع (باسورد الأدمن، Telegram token، MQTT...) في ملف متجاهَل من git
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Host.UseWindowsService(o => o.ServiceName = "XChemicalStorage");

var hostingUrls = builder.Configuration["Hosting:Urls"];
if (!string.IsNullOrWhiteSpace(hostingUrls))
    builder.WebHost.UseUrls(hostingUrls);

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

    options.UseSqlServer(connectionString)
       .AddInterceptors(new ReadingImmutabilityInterceptor());
});


builder.Services.AddControllersWithViews(); // Adds MVC services\
// ===== Permissions (DB-driven, see Authorization/ and Constants/Permissions.cs) =====
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PermissionCacheSignal>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
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
builder.Services.AddSingleton<StationState>();                       // local monitoring station (LAN)
builder.Services.AddHostedService<ConnectivityMonitorService>();

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
        // تطبيق الـ Migrations أوتوماتيك (Startup:AutoMigrate) — بيعمل الداتابيز لو مش موجودة
        if (app.Configuration.GetValue("Startup:AutoMigrate", true))
        {
            var migrateDb = services.GetRequiredService<ApplicationDbContext>();
            await migrateDb.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        await X_ChemicalStorage.Seeds.DefaultRoles.SeedAsync(userManager, roleManager);
        // مستخدمين التجربة (باسورد معروف) بيتعملوا في التطوير بس
        if (app.Configuration.GetValue("Startup:SeedDemoUsers", false))
        {
            await X_ChemicalStorage.Seeds.DefaultUsers.SeedUserAsync(userManager, roleManager);
            await X_ChemicalStorage.Seeds.DefaultUsers.SeedAdminAsync(userManager, roleManager);
        }
        // أدمن الموقع: الباسورد من appsettings.Local.json (AdminSeed:Password)
        await X_ChemicalStorage.Seeds.DefaultUsers.SeedConfiguredAdminAsync(
            userManager, app.Configuration.GetSection("AdminSeed"), logger);

        // Sync the permission catalog into ERP_Permissions (+ migrate old role claims).
        // Never block startup: if the migration isn't applied yet, Admin still has full access.
        try
        {
            await X_ChemicalStorage.Authorization.PermissionSeeder.SyncAsync(
                services.GetRequiredService<ApplicationDbContext>(), logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Permission sync skipped. Run 'Update-Database' to create the permission tables.");
        }

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

if (app.Configuration.GetValue("Hosting:UseHttpsRedirection", false))
    app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// فحص سريع بدون تسجيل دخول (للسكريبتات والمراقبة الخارجية). الحالة بس، من غير أي تفاصيل.
app.MapGet("/healthz", (IoTHealth health) =>
{
    var report = health.Evaluate(DateTime.UtcNow);
    return Results.Json(new { status = report.Status });
}).AllowAnonymous();

//app.Urls.Add("http://localhost:5000");

//var url = "http://localhost:5000";

//System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
//{
//    FileName = url,
//    UseShellExecute = true
//});

app.Run();