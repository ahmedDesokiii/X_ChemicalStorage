using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using X_ChemicalStorage.BioBank.Services;

namespace X_ChemicalStorage.Controllers
{
    /// <summary>
    /// Site settings for the local lab station:
    ///  - System page (biobank/settings): MQTT, notifications, backup, the signed-in user's password.
    ///  - Alarm page  (biobank/alarm-settings): per-freezer limits/delays, reminders, alarm contacts.
    /// Values are written to appsettings.Local.json (gitignored, next to the app). Most services read
    /// their options once at startup, so a restart applies them; on the lab PC (Windows Service) the
    /// Restart button exits with an error code and Windows restarts the service within seconds.
    /// </summary>
    [Authorize]
    public class SiteSettingsController : Controller
    {
        private const string LocalFileName = "appsettings.Local.json";
        private static readonly SemaphoreSlim FileLock = new(1, 1);

        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly UserManager<ApplicationUser> _users;
        private readonly ILogger<SiteSettingsController> _log;

        public SiteSettingsController(IConfiguration config, IWebHostEnvironment env, IHostApplicationLifetime lifetime,
            UserManager<ApplicationUser> users, ILogger<SiteSettingsController> log)
        {
            _config = config; _env = env; _lifetime = lifetime; _users = users; _log = log;
        }

        private static bool IsWindowsService =>
            Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService();

        private string LocalFilePath => Path.Combine(_env.ContentRootPath, LocalFileName);

        // ================================================================ pages
        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpGet("biobank/settings")]
        public IActionResult System() => View("~/Views/SiteSettings/System.cshtml");

        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpGet("biobank/alarm-settings")]
        public IActionResult Alarms() => View("~/Views/SiteSettings/Alarms.cshtml");

        // ================================================================ read
        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpGet("biobank/api/site-config")]
        public IActionResult GetConfig()
        {
            string S(string key, string def = "") => _config[key] ?? def;
            bool B(string key, bool def) => _config.GetValue(key, def);
            int I(string key, int def) => _config.GetValue(key, def);

            return Ok(new
            {
                station = new
                {
                    machineName = Environment.MachineName,
                    addresses = LocalIPv4(),
                    environment = _env.EnvironmentName,
                    runningAsService = IsWindowsService,
                    localSettingsFile = LocalFilePath,
                    localSettingsExists = global::System.IO.File.Exists(LocalFilePath),
                    urls = S("Hosting:Urls", "(Visual Studio)")
                },
                mqtt = new
                {
                    enabled = B("Mqtt:Enabled", true),
                    host = S("Mqtt:Host", "127.0.0.1"),
                    port = I("Mqtt:Port", 1883),
                    topic = S("Mqtt:Topic", "freezer/+/telemetry"),
                    username = S("Mqtt:Username"),
                    hasPassword = !string.IsNullOrEmpty(_config["Mqtt:Password"])
                },
                telegram = new
                {
                    enabled = B("Notifications:Telegram:Enabled", false),
                    hasToken = !string.IsNullOrEmpty(_config["Notifications:Telegram:BotToken"]),
                    tokenHint = Mask(_config["Notifications:Telegram:BotToken"])
                },
                email = new
                {
                    enabled = B("Notifications:Email:Enabled", false),
                    host = S("Notifications:Email:Host"),
                    port = I("Notifications:Email:Port", 587),
                    enableSsl = B("Notifications:Email:EnableSsl", true),
                    user = S("Notifications:Email:User"),
                    from = S("Notifications:Email:From"),
                    hasPassword = !string.IsNullOrEmpty(_config["Notifications:Email:Password"])
                },
                sms = new { enabled = B("Notifications:Sms:Enabled", false) },
                alarms = new
                {
                    notifyOnCleared = B("Notifications:NotifyOnCleared", true),
                    reminderEnabled = B("Notifications:Reminder:Enabled", true),
                    reminderIntervalMinutes = I("Notifications:Reminder:IntervalMinutes", 15),
                    maxReminders = I("Notifications:Reminder:MaxReminders", 20)
                },
                backup = new
                {
                    enabled = B("Backup:Enabled", true),
                    directory = S("Backup:Directory", @"D:\BioBank_Backups"),
                    secondaryDirectory = S("Backup:SecondaryDirectory"),
                    dailyTime = S("Backup:DailyTime", "02:00"),
                    keepDays = I("Backup:KeepDays", 30),
                    bioEnabled = B("Backup:BioBankEnabled", true),
                    bioEveryHours = I("Backup:BioBankEveryHours", 6)
                }
            });
        }

        // ================================================================ write
        public class SiteConfigUpdate
        {
            public bool? MqttEnabled { get; set; }
            public string? MqttHost { get; set; }
            public int? MqttPort { get; set; }
            public string? MqttTopic { get; set; }
            public string? MqttUsername { get; set; }
            public string? MqttPassword { get; set; }          // null/"" = keep
            public bool? MqttClearPassword { get; set; }

            public bool? TelegramEnabled { get; set; }
            public string? TelegramBotToken { get; set; }      // null/"" = keep

            public bool? EmailEnabled { get; set; }
            public string? EmailHost { get; set; }
            public int? EmailPort { get; set; }
            public bool? EmailEnableSsl { get; set; }
            public string? EmailUser { get; set; }
            public string? EmailPassword { get; set; }         // null/"" = keep
            public string? EmailFrom { get; set; }

            public bool? NotifyOnCleared { get; set; }
            public bool? ReminderEnabled { get; set; }
            public int? ReminderIntervalMinutes { get; set; }
            public int? MaxReminders { get; set; }

            public bool? BackupEnabled { get; set; }
            public string? BackupDirectory { get; set; }
            public string? BackupSecondaryDirectory { get; set; }
            public string? BackupDailyTime { get; set; }
            public int? BackupKeepDays { get; set; }
            public bool? BackupBioEnabled { get; set; }
            public int? BackupBioEveryHours { get; set; }
        }

        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpPut("biobank/api/site-config")]
        public async Task<IActionResult> SaveConfig([FromBody] SiteConfigUpdate u)
        {
            if (u == null) return BadRequest(new { message = "Nothing to save." });

            // ---- validation (plain messages the lab can act on)
            if (u.MqttPort is < 1 or > 65535) return BadRequest(new { message = "MQTT port must be 1-65535." });
            if (u.MqttHost != null && string.IsNullOrWhiteSpace(u.MqttHost)) return BadRequest(new { message = "MQTT host is required (127.0.0.1 when Mosquitto runs on this PC)." });
            if (u.MqttTopic != null && !u.MqttTopic.Trim().StartsWith("freezer/")) return BadRequest(new { message = "Topic must start with freezer/ (for example freezer/+/+)." });
            if (u.EmailPort is < 1 or > 65535) return BadRequest(new { message = "E-mail port must be 1-65535." });
            if (u.ReminderIntervalMinutes is < 1 or > 1440) return BadRequest(new { message = "Reminder interval must be 1-1440 minutes." });
            if (u.MaxReminders is < 0 or > 500) return BadRequest(new { message = "Max reminders must be 0-500." });
            if (u.BackupBioEveryHours is < 1 or > 168) return BadRequest(new { message = "Bio Bank backup interval must be 1-168 hours." });
            if (u.BackupKeepDays is < 1 or > 3650) return BadRequest(new { message = "Keep backups for 1-3650 days." });
            if (u.BackupDailyTime != null && !TimeSpan.TryParseExact(u.BackupDailyTime.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out _))
                return BadRequest(new { message = "Backup time must look like 02:00." });
            if (u.BackupEnabled != false && u.BackupDirectory != null && string.IsNullOrWhiteSpace(u.BackupDirectory))
                return BadRequest(new { message = "Backup folder is required while backups are on." });

            await FileLock.WaitAsync();
            try
            {
                JsonObject root;
                if (global::System.IO.File.Exists(LocalFilePath))
                {
                    var text = await global::System.IO.File.ReadAllTextAsync(LocalFilePath);
                    try
                    {
                        root = JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text, null,
                            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
                            ?? new JsonObject();
                    }
                    catch (JsonException)
                    {
                        return BadRequest(new { message = $"{LocalFileName} is not valid JSON. Fix or delete it, then save again." });
                    }
                }
                else root = new JsonObject();

                void Set(string path, JsonNode? value)
                {
                    var parts = path.Split(':');
                    var node = root;
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (node[parts[i]] is not JsonObject child) { child = new JsonObject(); node[parts[i]] = child; }
                        node = child;
                    }
                    node[parts[^1]] = value;
                }
                void SetIf(string path, object? v)
                {
                    if (v is bool b) Set(path, JsonValue.Create(b));
                    else if (v is int i) Set(path, JsonValue.Create(i));
                }
                void SetStr(string path, string? v) { if (v != null) Set(path, JsonValue.Create(v.Trim())); }
                void SetSecret(string path, string? v) { if (!string.IsNullOrEmpty(v)) Set(path, JsonValue.Create(v)); }

                SetIf("Mqtt:Enabled", u.MqttEnabled);
                SetStr("Mqtt:Host", u.MqttHost);
                SetIf("Mqtt:Port", u.MqttPort);
                SetStr("Mqtt:Topic", u.MqttTopic);
                SetStr("Mqtt:Username", u.MqttUsername);
                if (u.MqttClearPassword == true) Set("Mqtt:Password", JsonValue.Create(""));
                else SetSecret("Mqtt:Password", u.MqttPassword);

                SetIf("Notifications:Telegram:Enabled", u.TelegramEnabled);
                SetSecret("Notifications:Telegram:BotToken", u.TelegramBotToken?.Trim());

                SetIf("Notifications:Email:Enabled", u.EmailEnabled);
                SetStr("Notifications:Email:Host", u.EmailHost);
                SetIf("Notifications:Email:Port", u.EmailPort);
                SetIf("Notifications:Email:EnableSsl", u.EmailEnableSsl);
                SetStr("Notifications:Email:User", u.EmailUser);
                SetSecret("Notifications:Email:Password", u.EmailPassword);
                SetStr("Notifications:Email:From", u.EmailFrom);

                SetIf("Notifications:NotifyOnCleared", u.NotifyOnCleared);
                SetIf("Notifications:Reminder:Enabled", u.ReminderEnabled);
                SetIf("Notifications:Reminder:IntervalMinutes", u.ReminderIntervalMinutes);
                SetIf("Notifications:Reminder:MaxReminders", u.MaxReminders);

                SetIf("Backup:Enabled", u.BackupEnabled);
                SetStr("Backup:Directory", u.BackupDirectory);
                SetStr("Backup:SecondaryDirectory", u.BackupSecondaryDirectory);
                SetStr("Backup:DailyTime", u.BackupDailyTime);
                SetIf("Backup:KeepDays", u.BackupKeepDays);
                SetIf("Backup:BioBankEnabled", u.BackupBioEnabled);
                SetIf("Backup:BioBankEveryHours", u.BackupBioEveryHours);

                // write to a temp file first so a crash never leaves half a settings file
                var tmp = LocalFilePath + ".tmp";
                await global::System.IO.File.WriteAllTextAsync(tmp,
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                global::System.IO.File.Move(tmp, LocalFilePath, overwrite: true);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(500, new { message = $"No permission to write {LocalFilePath}." });
            }
            finally { FileLock.Release(); }

            _log.LogInformation("Site settings saved by {User}", User.Identity?.Name);
            return Ok(new
            {
                message = "Saved. Restart to apply.",
                restartRequired = true,
                canRestart = IsWindowsService
            });
        }

        // ================================================================ restart
        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpPost("biobank/api/site-config/restart")]
        public IActionResult Restart()
        {
            if (!IsWindowsService)
                return BadRequest(new { message = "Running from Visual Studio: stop and start it again (Shift+F5, then F5)." });

            _log.LogWarning("Restart requested from settings page by {User}", User.Identity?.Name);
            // Non-zero exit code = "stopped with error", so the service recovery actions
            // (set by 3-install-service.ps1) start it again within ~10 seconds.
            Environment.ExitCode = 1;
            _ = Task.Run(async () => { await Task.Delay(1500); _lifetime.StopApplication(); });
            return Ok(new { message = "Restarting. The page will reconnect in about 20 seconds." });
        }

        // ================================================================ password
        public class PasswordChange
        {
            public string? NewPassword { get; set; }
            public string? ConfirmPassword { get; set; }
        }

        [HttpPost("biobank/api/account/password")]
        public async Task<IActionResult> ChangePassword([FromBody] PasswordChange p)
        {
            if (string.IsNullOrEmpty(p?.NewPassword)) return BadRequest(new { message = "Enter the new password." });
            if (p.NewPassword != p.ConfirmPassword) return BadRequest(new { message = "The two passwords don't match." });

            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var result = await _users.ResetPasswordAsync(user, token, p.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

            _log.LogInformation("User {User} changed their password from the settings page", user.UserName);
            return Ok(new { message = "Password changed. Use it next time you sign in." });
        }

        // ================================================================ site setup (freezers, links, limits, contacts)
        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpGet("biobank/api/site-setup/export")]
        public async Task<IActionResult> ExportSiteSetup([FromServices] ISiteSetupService setup)
            => Ok(await setup.ExportAsync());

        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpPost("biobank/api/site-setup/import")]
        public async Task<IActionResult> ImportSiteSetup([FromServices] ISiteSetupService setup, [FromBody] SiteSetupFile file)
        {
            if (file?.Freezers == null || file.Freezers.Count == 0)
                return BadRequest(new { message = "That file has no freezers. Choose a site-setup.json exported from Settings." });
            var r = await setup.ImportAsync(file, User.Identity?.Name ?? "settings page");
            return Ok(new { message = r.Summary, problems = r.Problems });
        }

        // ================================================================ telegram chat finder
        /// <summary>
        /// Lists people who recently messaged the bot, so their chat ID can be picked from a list
        /// instead of reading Telegram's raw getUpdates output.
        /// </summary>
        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpGet("biobank/api/telegram/recent-chats")]
        public async Task<IActionResult> TelegramRecentChats([FromServices] IHttpClientFactory http)
        {
            var token = _config["Notifications:Telegram:BotToken"];
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { message = "Save the Telegram bot token on the System page first." });
            var baseUrl = (_config["Notifications:Telegram:ApiBaseUrl"] ?? "https://api.telegram.org").TrimEnd('/');

            JsonDocument doc;
            try
            {
                var client = http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                var json = await client.GetStringAsync($"{baseUrl}/bot{token}/getUpdates?limit=100");
                doc = JsonDocument.Parse(json);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == global::System.Net.HttpStatusCode.Unauthorized
                                                  || ex.StatusCode == global::System.Net.HttpStatusCode.NotFound)
            {
                return BadRequest(new { message = "Telegram rejected the bot token. Check it on the System page." });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return BadRequest(new { message = "Can't reach Telegram. This PC needs internet for this step." });
            }

            var chats = new Dictionary<long, object>();
            using (doc)
            {
                if (doc.RootElement.TryGetProperty("result", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var upd in results.EnumerateArray())
                    {
                        // a normal message, or a message in a group/channel the bot was added to
                        JsonElement msg = default;
                        foreach (var k in new[] { "message", "edited_message", "channel_post", "my_chat_member" })
                            if (upd.TryGetProperty(k, out msg)) break;
                        if (msg.ValueKind != JsonValueKind.Object || !msg.TryGetProperty("chat", out var chat)) continue;
                        if (!chat.TryGetProperty("id", out var idEl) || !idEl.TryGetInt64(out var id)) continue;

                        string Str(string p) => chat.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                        var name = (Str("first_name") + " " + Str("last_name")).Trim();
                        if (name.Length == 0) name = Str("title");
                        var text = msg.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

                        chats[id] = new { chatId = id.ToString(CultureInfo.InvariantCulture), name, username = Str("username"), type = Str("type"), lastText = text };
                    }
                }
            }
            return Ok(chats.Values);
        }

        // ================================================================ helpers
        private static string? Mask(string? secret) =>
            string.IsNullOrEmpty(secret) ? null : secret.Length <= 6 ? "••••" : "••••" + secret[^4..];

        private static List<string> LocalIPv4()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses
                        .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => $"{a.Address} ({n.Name})"))
                    .ToList();
            }
            catch { return new List<string>(); }
        }
    }
}
