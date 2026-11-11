using System.Collections.Concurrent;
using System.Data;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Live;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;

namespace X_ChemicalStorage.BioBank.Services
{
    /// <summary>
    /// Settings → Backup. Two independent kinds of backup, each automatic + manual, each can be uploaded and restored:
    ///   "full"    the whole application database (.bak, SQL Server native) + sample attachments zip
    ///   "biobank" only the Bio Bank data (.zip, see <see cref="BioBankDataArchive"/>), restorable without touching the rest
    /// Every restore first takes a safety copy of what is there now, pauses freezer data intake, and is logged.
    /// </summary>
    public class BackupCenterService
    {
        public const string Full = "full", BioBank = "biobank";

        private readonly ApplicationDbContext _db;
        private readonly IBackupService _fullBackup;
        private readonly IBackupExecutor _exec;
        private readonly BackupOptions _opt;
        private readonly MaintenanceState _maint;
        private readonly LiveBroadcaster _live;
        private readonly SystemNoticeService _notice;
        private readonly IServiceScopeFactory _scopes;
        private readonly IHostEnvironment _env;
        private readonly ILogger<BackupCenterService> _log;

        private static readonly ConcurrentDictionary<string, (DateTime Mtime, BioBankDataArchive.Manifest? M)> ManifestCache = new();
        private static readonly SemaphoreSlim ActivityLock = new(1, 1);

        public BackupCenterService(ApplicationDbContext db, IBackupService fullBackup, IBackupExecutor exec, IOptions<BackupOptions> opt,
            MaintenanceState maint, LiveBroadcaster live, SystemNoticeService notice, IServiceScopeFactory scopes,
            IHostEnvironment env, ILogger<BackupCenterService> log)
        {
            _db = db; _fullBackup = fullBackup; _exec = exec; _opt = opt.Value; _maint = maint; _live = live;
            _notice = notice; _scopes = scopes; _env = env; _log = log;
        }

        public BackupOptions Options => _opt;
        public string FullDir => _opt.Directory;
        public string BioDir => Path.Combine(_opt.Directory, string.IsNullOrWhiteSpace(_opt.BioBankFolder) ? "BioBank" : _opt.BioBankFolder);
        public string AttachmentsRoot => !string.IsNullOrWhiteSpace(_opt.AttachmentsDirectory)
            ? _opt.AttachmentsDirectory! : Path.Combine(_env.ContentRootPath, "App_Data", "biobank-uploads");
        private string ActivityFile => Path.Combine(_opt.Directory, "backup-activity.jsonl");
        private string DbFilePrefix => SafeFileName(_exec.DatabaseName) + "_";

        public static bool IsScope(string? s) => s is Full or BioBank;

        // ================================================================== overview
        public async Task<object> OverviewAsync()
        {
            var full = await _fullBackup.GetStatusAsync();
            var fullFiles = ListFiles(Full);
            var bioFiles = ListFiles(BioBank);
            var lastBio = bioFiles.FirstOrDefault(f => f.Type is "Auto" or "Manual");
            var lastBioAuto = bioFiles.FirstOrDefault(f => f.Type == "Auto");
            var now = DateTime.UtcNow;

            string? bioWarning = null;
            if (_opt.BioBankEnabled)
            {
                if (lastBio == null) bioWarning = "No Bio Bank backup yet.";
                else if ((now - lastBio.ModifiedUtc).TotalHours > Math.Max(_opt.BioBankEveryHours * 2, 12))
                    bioWarning = $"Last Bio Bank backup was {Math.Round((now - lastBio.ModifiedUtc).TotalHours)} hours ago.";
            }

            return new
            {
                serverTimeUtc = now,
                maintenance = new { active = _maint.Active, reason = _maint.Reason, since = _maint.Since },
                canRestart = WindowsServiceHelpers.IsWindowsService(),
                folders = new { main = _opt.Directory, biobank = BioDir, secondary = string.IsNullOrWhiteSpace(_opt.SecondaryDirectory) ? null : _opt.SecondaryDirectory, attachments = AttachmentsRoot },
                uploadMaxMegabytes = _opt.UploadMaxMegabytes,
                full = new
                {
                    enabled = full.Enabled,
                    schedule = $"Every day at {_opt.DailyTime}",
                    nextRunLocal = full.NextRunLocal,
                    lastSuccessAt = full.LastSuccessAt,
                    warning = full.Warning,
                    database = full.DatabaseName,
                    keepDays = _opt.KeepDays,
                    files = fullFiles,
                    totalBytes = fullFiles.Sum(f => f.SizeBytes + (f.AttachmentsBytes ?? 0))
                },
                biobank = new
                {
                    enabled = _opt.BioBankEnabled,
                    schedule = $"Every {_opt.BioBankEveryHours} hours",
                    nextRunUtc = !_opt.BioBankEnabled ? (DateTime?)null
                        : lastBioAuto == null ? now : lastBioAuto.ModifiedUtc.AddHours(Math.Max(1, _opt.BioBankEveryHours)),
                    lastSuccessAt = lastBio?.ModifiedUtc,
                    warning = bioWarning,
                    keepDays = _opt.KeepDays,
                    files = bioFiles,
                    totalBytes = bioFiles.Sum(f => f.SizeBytes)
                },
                activity = await ReadActivityAsync(40)
            };
        }

        public sealed class BackupFile
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";          // Auto | Manual | PreRestore | Uploaded
            public long SizeBytes { get; set; }
            public DateTime ModifiedUtc { get; set; }
            public string? AttachmentsFile { get; set; }
            public long? AttachmentsBytes { get; set; }
            public Dictionary<string, int>? Summary { get; set; }
            public string? CreatedBy { get; set; }
            public bool Readable { get; set; } = true;
        }

        public List<BackupFile> ListFiles(string scope)
        {
            var list = new List<BackupFile>();
            try
            {
                if (scope == Full)
                {
                    if (!Directory.Exists(FullDir)) return list;
                    foreach (var f in new DirectoryInfo(FullDir).GetFiles("*.bak"))
                    {
                        var zipName = Path.GetFileNameWithoutExtension(f.Name) + "_attachments.zip";
                        var zip = new FileInfo(Path.Combine(FullDir, zipName));
                        list.Add(new BackupFile
                        {
                            Name = f.Name, Type = TypeFromName(f.Name), SizeBytes = f.Length, ModifiedUtc = f.LastWriteTimeUtc,
                            AttachmentsFile = zip.Exists ? zip.Name : null, AttachmentsBytes = zip.Exists ? zip.Length : null
                        });
                    }
                }
                else
                {
                    if (!Directory.Exists(BioDir)) return list;
                    foreach (var f in new DirectoryInfo(BioDir).GetFiles("BioBank_*.zip"))
                    {
                        var m = CachedManifest(f);
                        list.Add(new BackupFile
                        {
                            Name = f.Name, Type = TypeFromName(f.Name), SizeBytes = f.Length, ModifiedUtc = f.LastWriteTimeUtc,
                            Summary = m?.Summary, CreatedBy = m?.CreatedBy, Readable = m != null
                        });
                    }
                }
            }
            catch (Exception ex) { _log.LogWarning("Could not list {Scope} backups: {Msg}", scope, ex.Message); }
            return list.OrderByDescending(f => f.ModifiedUtc).ToList();
        }

        private static BioBankDataArchive.Manifest? CachedManifest(FileInfo f)
        {
            if (ManifestCache.TryGetValue(f.FullName, out var c) && c.Mtime == f.LastWriteTimeUtc) return c.M;
            BioBankDataArchive.Manifest? m = null;
            try { m = BioBankDataArchive.ReadManifest(f.FullName); } catch { }
            ManifestCache[f.FullName] = (f.LastWriteTimeUtc, m);
            return m;
        }

        private static string TypeFromName(string name)
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var last = stem.Substring(stem.LastIndexOf('_') + 1);
            return last is "Auto" or "Manual" or "PreRestore" or "Uploaded" ? last : "Manual";
        }

        /// A file that really is one of ours, inside the scope's folder (never a path from the browser).
        public string? ResolveFile(string scope, string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)) return null;
            var dir = scope == Full ? FullDir : BioDir;
            var ok = scope == Full
                ? name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_attachments.zip", StringComparison.OrdinalIgnoreCase)
                : name.StartsWith("BioBank_", StringComparison.Ordinal) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            if (!ok) return null;
            var path = Path.Combine(dir, name);
            return File.Exists(path) ? path : null;
        }

        // ================================================================== Bio Bank backup
        public async Task<BackupResultDto> RunBioBankAsync(string type, string triggeredBy, CancellationToken ct = default)
        {
            type = BackupLocks.NormalizeType(type);
            if (!await BackupLocks.Gate.WaitAsync(type == "PreRestore" ? TimeSpan.FromMinutes(2) : TimeSpan.Zero, ct))
                return new BackupResultDto { Code = "busy", Message = "Another backup or restore is running. Please wait for it to finish." };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? name = null, path = null;
            try
            {
                Directory.CreateDirectory(BioDir);
                name = $"BioBank_{ToLocal(DateTime.UtcNow):yyyyMMdd_HHmmss}_{type}.zip";
                path = Path.Combine(BioDir, name);
                var m = await BioBankDataArchive.WriteAsync(_db, path, AttachmentsRoot, type, triggeredBy, ct);
                BioBankDataArchive.ReadManifest(path);                       // read it back: a backup we cannot open is no backup

                var copied = false;
                if (!string.IsNullOrWhiteSpace(_opt.SecondaryDirectory))
                {
                    try
                    {
                        var dir2 = Path.Combine(_opt.SecondaryDirectory!, Path.GetFileName(BioDir));
                        Directory.CreateDirectory(dir2);
                        File.Copy(path, Path.Combine(dir2, name), true);
                        copied = true;
                        Retention(dir2, "BioBank_*.zip");
                    }
                    catch (Exception ex) { _log.LogWarning("Secondary copy of the Bio Bank backup failed: {Msg}", ex.Message); }
                }
                Retention(BioDir, "BioBank_*.zip");

                var size = new FileInfo(path).Length;
                await LogActivityAsync(BioBank, "backup", name, triggeredBy, true,
                    $"{type}: {m.Summary.GetValueOrDefault("freezers")} freezers, {m.Summary.GetValueOrDefault("samples")} samples, {m.Summary.GetValueOrDefault("readings")} readings, {m.AttachmentFiles} attachments");
                return new BackupResultDto
                {
                    Success = true, Code = "ok", Message = "Bio Bank backup completed.", FileName = name, FilePath = path,
                    SizeBytes = size, DurationSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1), Verified = true, CopiedToSecondary = copied
                };
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Bio Bank backup ({Type}) failed", type);
                try { if (path != null && File.Exists(path)) File.Delete(path); } catch { }
                var msg = ex.GetBaseException().Message;
                await LogActivityAsync(BioBank, "backup", name, triggeredBy, false, msg);
                if (type == "Auto")
                    try { await _notice.NotifyAsync("[BioBank] BIO BANK BACKUP FAILED", "[BioBank] Automatic Bio Bank backup FAILED. " + Trunc(msg, 160), DateTime.UtcNow, ct); } catch { }
                return new BackupResultDto { Code = "failed", Message = msg, DurationSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1) };
            }
            finally { BackupLocks.Gate.Release(); }
        }

        private void Retention(string dir, string pattern)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _opt.KeepDays));
                var files = new DirectoryInfo(dir).GetFiles(pattern).OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                foreach (var f in files.Skip(Math.Max(1, _opt.KeepMinimumFiles)).Where(f => f.LastWriteTimeUtc < cutoff))
                    try { f.Delete(); } catch { }
            }
            catch (Exception ex) { _log.LogWarning("Retention skipped for {Dir}: {Msg}", dir, ex.Message); }
        }

        // ================================================================== upload
        public async Task<(bool Ok, string Message, string? FileName)> SaveUploadAsync(string scope, IFormFile file, string user, CancellationToken ct)
        {
            if (file == null || file.Length == 0) return (false, "Choose a backup file first.", null);
            if (file.Length > (long)_opt.UploadMaxMegabytes * 1024 * 1024) return (false, $"The file is larger than {_opt.UploadMaxMegabytes} MB.", null);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (scope == Full && ext != ".bak") return (false, "A full application backup is a .bak file.", null);
            if (scope == BioBank && ext != ".zip") return (false, "A Bio Bank backup is a .zip file (BioBank_….zip).", null);

            var dir = scope == Full ? FullDir : BioDir;
            Directory.CreateDirectory(dir);
            var stamp = ToLocal(DateTime.UtcNow).ToString("yyyyMMdd_HHmmss");
            var name = scope == Full ? $"{DbFilePrefix}{stamp}_Uploaded.bak" : $"BioBank_{stamp}_Uploaded.zip";
            var path = Path.Combine(dir, name);
            var tmp = path + ".upload";

            try
            {
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
                    await file.CopyToAsync(fs, ct);

                if (scope == Full)
                {
                    File.Move(tmp, path, overwrite: true);
                    try { await VerifyBakAsync(path, ct); }
                    catch { File.Delete(path); throw; }
                }
                else
                {
                    BioBankDataArchive.ReadManifest(tmp);
                    File.Move(tmp, path, overwrite: true);
                }

                await LogActivityAsync(scope, "upload", name, user, true, $"from {Path.GetFileName(file.FileName)} ({file.Length / 1024 / 1024.0:0.0} MB)");
                return (true, "Uploaded and checked. You can restore it now.", name);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                var msg = Friendly(ex);
                await LogActivityAsync(scope, "upload", Path.GetFileName(file.FileName), user, false, msg);
                return (false, "This file cannot be used: " + msg, null);
            }
        }

        // ================================================================== restore
        public async Task<(bool Ok, string Message, object? Details)> RestoreAsync(string scope, string fileName, string user, CancellationToken ct)
        {
            var path = ResolveFile(scope, fileName);
            if (path == null || (scope == Full && !fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)))
                return (false, "Backup file not found.", null);

            if (!_maint.TryBegin($"Restoring {(scope == Full ? "the whole application" : "Bio Bank data")} from {fileName}"))
                return (false, "A restore is already running.", null);

            await _live.MaintenanceAsync(true, _maint.Reason);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? safety = null;
            try
            {
                // 1) safety copy of what is there now
                var pre = scope == Full ? await _fullBackup.RunAsync("PreRestore", user, ct) : await RunBioBankAsync("PreRestore", user, ct);
                if (!pre.Success)
                    return (false, "Restore stopped before changing anything: the safety copy of the current data failed. " + pre.Message, null);
                safety = pre.FileName;

                // 2) restore
                if (!await BackupLocks.Gate.WaitAsync(TimeSpan.FromMinutes(5), ct))
                    return (false, "Another backup is running. Try again in a minute.", null);
                object details;
                try
                {
                    details = scope == Full ? await RestoreFullAsync(path, ct) : await RestoreBioBankAsync(path, ct);
                }
                finally { BackupLocks.Gate.Release(); }

                await LogActivityAsync(scope, "restore", fileName, user, true, $"safety copy: {safety} · {sw.Elapsed.TotalSeconds:0} s");
                _log.LogWarning("{Scope} restored from {File} by {User} (safety copy {Safety})", scope, fileName, user, safety);
                return (true, "Restore completed.", new { safetyCopy = safety, seconds = Math.Round(sw.Elapsed.TotalSeconds, 1), details });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Restore of {Scope} from {File} failed", scope, fileName);
                var msg = Friendly(ex);
                await LogActivityAsync(scope, "restore", fileName, user, false, msg + (safety != null ? $" · safety copy: {safety}" : ""));
                return (false, "Restore failed: " + msg + (safety != null ? $" Your data before the restore is saved as {safety}." : ""), null);
            }
            finally
            {
                _maint.End();
                await _live.MaintenanceAsync(false, null);
                _live.StationChanged();
            }
        }

        private async Task<object> RestoreBioBankAsync(string path, CancellationToken ct)
        {
            var m = await BioBankDataArchive.RestoreDataAsync(_db, path, ct);
            int files = 0; string? warn = null;
            try { files = BioBankDataArchive.RestoreAttachments(path, AttachmentsRoot); }
            catch (Exception ex) { warn = "Data restored, but the attachments folder could not be replaced: " + ex.Message; }
            _db.ChangeTracker.Clear();
            return new { from = m.CreatedUtc, tables = m.Tables.Count, rows = m.Tables.Sum(t => t.Rows), attachments = files, warning = warn, restartRecommended = false };
        }

        private async Task<object> RestoreFullAsync(string bakPath, CancellationToken ct)
        {
            var dbName = _exec.DatabaseName;
            var csb = new SqlConnectionStringBuilder(_db.Database.GetConnectionString()) { InitialCatalog = "master", Pooling = false };

            await using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            async Task<DataTable> Query(string sql, params (string, object)[] p)
            {
                await using var c = new SqlCommand(sql, conn) { CommandTimeout = _opt.CommandTimeoutSeconds };
                foreach (var (n, v) in p) c.Parameters.AddWithValue(n, v);
                var dt = new DataTable();
                await using var r = await c.ExecuteReaderAsync(ct);
                dt.Load(r);
                return dt;
            }
            async Task Exec(string sql, params (string, object)[] p)
            {
                await using var c = new SqlCommand(sql, conn) { CommandTimeout = _opt.CommandTimeoutSeconds };
                foreach (var (n, v) in p) c.Parameters.AddWithValue(n, v);
                await c.ExecuteNonQueryAsync(ct);
            }

            await Exec("RESTORE VERIFYONLY FROM DISK = @p", ("@p", bakPath));

            // put every file of the backup where this database's files live now (backups from another PC have other paths)
            var inBak = await Query("RESTORE FILELISTONLY FROM DISK = @p", ("@p", bakPath));
            var now = await Query("SELECT name, physical_name, type FROM sys.master_files WHERE database_id = DB_ID(@d)", ("@d", dbName));
            var dataNow = now.Rows.Cast<DataRow>().Where(r => Convert.ToInt32(r["type"]) == 0).Select(r => (string)r["physical_name"]).ToList();
            var logNow = now.Rows.Cast<DataRow>().Where(r => Convert.ToInt32(r["type"]) == 1).Select(r => (string)r["physical_name"]).ToList();
            if (dataNow.Count == 0) throw new InvalidOperationException($"Database {dbName} was not found on this SQL Server.");

            var moves = new List<(string Logical, string Physical)>();
            int di = 0, li = 0;
            foreach (DataRow r in inBak.Rows)
            {
                var logical = (string)r["LogicalName"];
                var isLog = string.Equals(Convert.ToString(r["Type"]), "L", StringComparison.OrdinalIgnoreCase);
                var pool = isLog ? logNow : dataNow;
                var idx = isLog ? li++ : di++;
                string target = idx < pool.Count ? pool[idx]
                    : Path.Combine(Path.GetDirectoryName(dataNow[0])!, $"{dbName}_{SafeFileName(logical)}{(isLog ? ".ldf" : ".ndf")}");
                moves.Add((logical, target));
            }

            var q = "[" + dbName.Replace("]", "]]") + "]";
            var sql = $"RESTORE DATABASE {q} FROM DISK = @p WITH REPLACE, RECOVERY"
                      + string.Concat(moves.Select((m, i) => $", MOVE @l{i} TO @f{i}"));
            var prm = new List<(string, object)> { ("@p", bakPath) };
            for (int i = 0; i < moves.Count; i++) { prm.Add(($"@l{i}", moves[i].Logical)); prm.Add(($"@f{i}", moves[i].Physical)); }

            // OFFLINE (not SINGLE_USER): no background worker can sneak in and take the one allowed connection
            await Exec($"ALTER DATABASE {q} SET OFFLINE WITH ROLLBACK IMMEDIATE");
            var restored = false;
            try
            {
                await Exec(sql, prm.ToArray());
                restored = true;
                await Exec($"ALTER DATABASE {q} SET MULTI_USER");
            }
            finally
            {
                if (!restored)
                    try { await Exec($"ALTER DATABASE {q} SET ONLINE"); } catch (Exception ex) { _log.LogError(ex, "Could not bring {Db} back ONLINE after a failed restore", dbName); }
                SqlConnection.ClearAllPools();
            }

            // older backup → bring the schema up to this version of the app
            string[] applied;
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                applied = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
                if (applied.Length > 0) await db.Database.MigrateAsync(ct);
            }

            // sample attachments taken together with this .bak
            int files = 0; string? warn = null;
            var zip = Path.Combine(Path.GetDirectoryName(bakPath)!, Path.GetFileNameWithoutExtension(bakPath) + "_attachments.zip");
            if (File.Exists(zip))
            {
                try { files = RestoreAttachmentsZip(zip); }
                catch (Exception ex) { warn = "Database restored, but the attachments could not be replaced: " + ex.Message; }
            }

            return new { database = dbName, migrationsApplied = applied, attachments = files, warning = warn, restartRecommended = true };
        }

        /// The full backup's attachments zip has the files at its root (ZipFile.CreateFromDirectory).
        private int RestoreAttachmentsZip(string zipPath)
        {
            var root = AttachmentsRoot;
            var parent = Path.GetDirectoryName(Path.GetFullPath(root))!;
            var staging = Path.Combine(parent, "biobank-uploads.restoring");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            ZipFile.ExtractToDirectory(zipPath, staging);
            var old = Path.Combine(parent, "biobank-uploads.before-restore-" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            if (Directory.Exists(root)) Directory.Move(root, old);
            Directory.Move(staging, root);
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count();
        }

        private async Task VerifyBakAsync(string path, CancellationToken ct)
        {
            var csb = new SqlConnectionStringBuilder(_db.Database.GetConnectionString()) { InitialCatalog = "master" };
            await using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);
            await using var c = new SqlCommand("RESTORE VERIFYONLY FROM DISK = @p", conn) { CommandTimeout = _opt.CommandTimeoutSeconds };
            c.Parameters.AddWithValue("@p", path);
            await c.ExecuteNonQueryAsync(ct);
        }

        // ================================================================== activity log
        public sealed class Activity
        {
            public DateTime At { get; set; }
            public string Scope { get; set; } = "";
            public string Action { get; set; } = "";
            public string? File { get; set; }
            public string? User { get; set; }
            public bool Ok { get; set; }
            public string? Message { get; set; }
        }

        public async Task LogActivityAsync(string scope, string action, string? file, string? user, bool ok, string? message)
        {
            await ActivityLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(_opt.Directory);
                var line = JsonSerializer.Serialize(new Activity { At = DateTime.UtcNow, Scope = scope, Action = action, File = file, User = user, Ok = ok, Message = Trunc(message, 600) });
                await File.AppendAllTextAsync(ActivityFile, line + Environment.NewLine);
            }
            catch (Exception ex) { _log.LogWarning("Could not write the backup activity log: {Msg}", ex.Message); }
            finally { ActivityLock.Release(); }
        }

        private async Task<List<Activity>> ReadActivityAsync(int take)
        {
            var list = new List<Activity>();
            try
            {
                if (File.Exists(ActivityFile))
                {
                    var lines = await File.ReadAllLinesAsync(ActivityFile);
                    foreach (var l in lines.Reverse().Take(take))
                        try { var a = JsonSerializer.Deserialize<Activity>(l); if (a != null) list.Add(a); } catch { }
                }
            }
            catch { }

            // full-database backups are also in the database log (older than the activity file, or taken before this version)
            try
            {
                var rows = (await _db.BackupLogs.AsNoTracking().OrderByDescending(l => l.StartedAt).Take(take).ToListAsync())
                    .Where(l => !list.Any(a => a.Scope == Full && a.Action == "backup" && a.File == l.FileName));
                list.AddRange(rows.Select(l => new Activity
                {
                    At = l.StartedAt, Scope = Full, Action = "backup", File = l.FileName, User = l.TriggeredBy,
                    Ok = l.Status == "Success", Message = l.Status == "Success" ? $"{l.Type}{(l.Verified ? " · verified" : "")}" : l.Error
                }));
            }
            catch { }
            return list.OrderByDescending(a => a.At).Take(take).ToList();
        }

        // ================================================================== helpers
        private DateTime ToLocal(DateTime utc)
        {
            foreach (var id in new[] { _opt.TimeZoneId, "Africa/Cairo", "Egypt Standard Time" })
                try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(id)); } catch { }
            return utc.AddHours(2);
        }

        private static string SafeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private static string? Trunc(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];

        private static string Friendly(Exception ex)
        {
            var m = ex.GetBaseException().Message;
            if (m.Contains("Operating system error 5", StringComparison.OrdinalIgnoreCase))
                m += " (SQL Server cannot read the backup folder: run deploy\\1-prepare-machine.ps1 again.)";
            else if (m.Contains("is terminating abnormally", StringComparison.OrdinalIgnoreCase) && m.Contains("format", StringComparison.OrdinalIgnoreCase))
                m = "The file is not a SQL Server backup, or it is damaged.";
            else if (m.Contains("newer", StringComparison.OrdinalIgnoreCase) && m.Contains("version", StringComparison.OrdinalIgnoreCase))
                m += " (The backup comes from a newer SQL Server than the one on this PC.)";
            return Trunc(m, 700)!;
        }
    }

    /// Takes the Bio Bank backup every N hours (Backup:BioBankEveryHours). Catches up after the PC was off.
    public class BioBankBackupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly BackupOptions _opt;
        private readonly ILogger<BioBankBackupWorker> _log;
        private DateTime _lastFailure = DateTime.MinValue;

        public BioBankBackupWorker(IServiceScopeFactory scopes, IOptions<BackupOptions> opt, ILogger<BioBankBackupWorker> log)
        { _scopes = scopes; _opt = opt.Value; _log = log; }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (!_opt.Enabled || !_opt.BioBankEnabled)
            {
                _log.LogInformation("Automatic Bio Bank backup is off.");
                return;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(75), ct); } catch (OperationCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var center = scope.ServiceProvider.GetRequiredService<BackupCenterService>();
                    var maint = scope.ServiceProvider.GetRequiredService<MaintenanceState>();
                    var lastAuto = center.ListFiles(BackupCenterService.BioBank).FirstOrDefault(f => f.Type == "Auto");
                    var due = lastAuto == null || DateTime.UtcNow - lastAuto.ModifiedUtc >= TimeSpan.FromHours(Math.Max(1, _opt.BioBankEveryHours));
                    var backoff = DateTime.UtcNow - _lastFailure < TimeSpan.FromMinutes(Math.Max(5, _opt.RetryAfterMinutes));
                    if (due && !backoff && !maint.Active)
                    {
                        var r = await center.RunBioBankAsync("Auto", "system", ct);
                        if (r.Success) _log.LogInformation("Automatic Bio Bank backup OK: {File}", r.FileName);
                        else if (r.Code != "busy") { _lastFailure = DateTime.UtcNow; _log.LogError("Automatic Bio Bank backup failed: {Msg}", r.Message); }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { _lastFailure = DateTime.UtcNow; _log.LogError(ex, "Bio Bank backup scheduler cycle failed"); }

                try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
