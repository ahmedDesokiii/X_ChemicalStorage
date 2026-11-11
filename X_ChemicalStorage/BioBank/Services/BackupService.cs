using System.IO.Compression;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Options;

namespace X_ChemicalStorage.BioBank.Services
{
    public class BackupService : IBackupService
    {
        // نسخة واحدة في نفس الوقت على مستوى التطبيق كله (مشترك مع نسخ البايو بانك والاسترجاع)
        private static SemaphoreSlim Gate => BackupLocks.Gate;

        private readonly ApplicationDbContext _db;
        private readonly IBackupExecutor _exec;
        private readonly BackupOptions _opt;
        private readonly SystemNoticeService _notice;
        private readonly ILogger<BackupService> _log;
        private readonly Func<DateTime> _clock;
        private readonly IHostEnvironment? _env;

        public BackupService(ApplicationDbContext db, IBackupExecutor exec, IOptions<BackupOptions> opt,
            SystemNoticeService notice, ILogger<BackupService> log, Func<DateTime>? clock = null, IHostEnvironment? env = null)
        {
            _db = db; _exec = exec; _opt = opt.Value; _notice = notice; _log = log; _env = env;
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        // ============================================================ تشغيل نسخة
        public async Task<BackupResultDto> RunAsync(string type, string triggeredBy, CancellationToken ct = default)
        {
            type = BackupLocks.NormalizeType(type);

            if (!_opt.Enabled && type == "Auto")
                return new BackupResultDto { Code = "disabled", Message = "Backup is disabled in settings." };

            if (!await Gate.WaitAsync(type == "PreRestore" ? TimeSpan.FromMinutes(2) : TimeSpan.Zero, ct))
                return new BackupResultDto { Code = "busy", Message = "A backup is already running. Please wait for it to finish." };

            try
            {
                var started = _clock();

                if (type == "Manual" && _opt.ManualCooldownSeconds > 0)
                {
                    var lastManual = (await _db.BackupLogs.AsNoTracking().Where(l => l.Type == "Manual").ToListAsync(ct))
                        .OrderByDescending(l => l.StartedAt).FirstOrDefault();
                    if (lastManual != null && (started - lastManual.StartedAt).TotalSeconds < _opt.ManualCooldownSeconds)
                        return new BackupResultDto { Code = "cooldown", Message = $"A backup was just taken. Please wait {_opt.ManualCooldownSeconds} seconds between manual backups." };
                }

                var sw = Stopwatch.StartNew();
                string? path = null, name = null, error = null, zipName = null;
                long? size = null;
                bool verified = false, copied = false, fileCreated = false;
                var note = new List<string>();

                try
                {
                    EnsureFolder(_opt.Directory, ct);

                    var dbName = _exec.DatabaseName;
                    name = $"{SafeFileName(dbName)}_{ToLocal(started):yyyyMMdd_HHmmss}_{type}.bak";
                    path = Path.Combine(_opt.Directory, name);

                    await _exec.BackupAsync(path, _opt.UseCompression, _opt.CommandTimeoutSeconds, ct);
                    fileCreated = true;
                    await _exec.VerifyAsync(path, _opt.CommandTimeoutSeconds, ct);
                    verified = true;

                    size = TryFileSize(path);
                    if (_opt.IncludeAttachments) zipName = TryZipAttachments(name, note);
                    copied = TrySecondaryCopy(path, name, zipName, note);
                    ApplyRetention(_opt.Directory, dbName, started, note);
                    if (!string.IsNullOrWhiteSpace(_opt.SecondaryDirectory))
                        ApplyRetention(_opt.SecondaryDirectory!, dbName, started, note);
                }
                catch (Exception ex)
                {
                    error = Friendly(ex);
                    _log.LogError(ex, "Database backup ({Type}) failed", type);
                    if (fileCreated && !verified) TryDelete(path);      // نسخة ما اتأكدناش إنها سليمة: مش هنسيبها
                }

                sw.Stop();
                var ok = error == null;

                var row = new Bio_BackupLog
                {
                    StartedAt = started,
                    FinishedAt = _clock(),
                    Type = type,
                    Status = ok ? "Success" : "Failed",
                    FilePath = ok ? path : null,
                    FileName = ok ? name : null,
                    SizeBytes = size,
                    DurationMs = (int)Math.Min(int.MaxValue, sw.ElapsedMilliseconds),
                    Verified = verified,
                    CopiedToSecondary = copied,
                    AttachmentsFileName = ok ? zipName : null,
                    Error = error == null ? null : Truncate(error, 1000),
                    TriggeredBy = Truncate(triggeredBy, 100)
                };
                try { _db.BackupLogs.Add(row); await _db.SaveChangesAsync(ct); }
                catch (Exception ex) { _log.LogError(ex, "Could not write the backup log row"); }

                if (!ok && type == "Auto")
                {
                    try
                    {
                        await _notice.NotifyAsync("[BioBank] BACKUP FAILED",
                            $"[BioBank] Automatic database BACKUP FAILED ({ToLocal(started):dd/MM HH:mm}). {Truncate(error, 160)}", started, ct);
                    }
                    catch (Exception ex) { _log.LogError(ex, "Could not queue the backup-failure notice"); }
                }

                return new BackupResultDto
                {
                    Success = ok,
                    Code = ok ? "ok" : "failed",
                    Message = ok ? "Backup completed." + (note.Count > 0 ? " " + string.Join(" ", note) : "") : error!,
                    FileName = ok ? name : null,
                    FilePath = ok ? path : null,
                    SizeBytes = size,
                    DurationSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1),
                    Verified = verified,
                    CopiedToSecondary = copied,
                    AttachmentsFileName = ok ? zipName : null
                };
            }
            finally { Gate.Release(); }
        }

        // ============================================================ حالة النسخ
        public async Task<BackupStatusDto> GetStatusAsync()
        {
            var now = _clock();
            var logs = (await _db.BackupLogs.AsNoTracking().ToListAsync()).OrderByDescending(l => l.StartedAt).ToList();
            var lastOk = logs.FirstOrDefault(l => l.Status == "Success");
            var last = logs.FirstOrDefault();

            var dto = new BackupStatusDto
            {
                Enabled = _opt.Enabled,
                DatabaseName = _exec.DatabaseName,
                Directory = _opt.Directory,
                SecondaryDirectory = string.IsNullOrWhiteSpace(_opt.SecondaryDirectory) ? null : _opt.SecondaryDirectory,
                IsLocalServer = BackupScheduler.IsLocalServer(_exec.DataSource),
                DailyTime = _opt.DailyTime,
                KeepDays = _opt.KeepDays,
                NextRunLocal = _opt.Enabled ? BackupScheduler.NextSlot(ToLocal(now), BackupScheduler.ParseDailyTime(_opt.DailyTime)) : null,
                LastSuccessAt = lastOk?.StartedAt,
                HoursSinceLastSuccess = lastOk == null ? null : Math.Round((now - lastOk.StartedAt).TotalHours, 1),
                LastBackup = last == null ? null : Map(last),
                History = logs.Take(10).Select(Map).ToList()
            };

            if (lastOk == null) dto.Warning = "No successful backup yet.";
            else if (dto.HoursSinceLastSuccess > _opt.StaleWarningHours) dto.Warning = $"Last successful backup was {Math.Round(dto.HoursSinceLastSuccess!.Value)} hours ago.";
            else if (last != null && last.Status == "Failed") dto.Warning = "The last backup attempt failed: " + Truncate(last.Error, 200);

            return dto;
        }

        // ============================================================ helpers
        private void EnsureFolder(string dir, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dir)) throw new InvalidOperationException("Backup folder is not configured (Backup:Directory).");
            try { System.IO.Directory.CreateDirectory(dir); return; }
            catch (Exception localEx)
            {
                // التطبيق مش على نفس جهاز SQL Server: خلّي SQL Server نفسه يعمل الفولدر
                try { _exec.EnsureDirectoryAsync(dir, ct).GetAwaiter().GetResult(); }
                catch (Exception sqlEx)
                {
                    throw new InvalidOperationException(
                        $"Backup folder '{dir}' could not be created ({localEx.GetBaseException().Message}; SQL Server: {sqlEx.GetBaseException().Message}). " +
                        "Check that the drive exists and the SQL Server service account can write to it.");
                }
            }
        }

        private string? AttachmentsRoot =>
            !string.IsNullOrWhiteSpace(_opt.AttachmentsDirectory) ? _opt.AttachmentsDirectory
            : _env != null ? Path.Combine(_env.ContentRootPath, "App_Data", "biobank-uploads") : null;

        /// ZIP لمرفقات العينات بنفس طابع الوقت. فشله ما بيفشلش النسخة (بس تحذير).
        private string? TryZipAttachments(string bakName, List<string> note)
        {
            var root = AttachmentsRoot;
            string? zipPath = null;
            try
            {
                if (root == null || !System.IO.Directory.Exists(root) || !System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
                    return null;
                var zipName = bakName.Substring(0, bakName.Length - ".bak".Length) + "_attachments.zip";
                zipPath = Path.Combine(_opt.Directory, zipName);
                ZipFile.CreateFromDirectory(root, zipPath, CompressionLevel.Fastest, false);
                return zipName;
            }
            catch (Exception ex)
            {
                _log.LogWarning("Attachments archive failed: {Msg}", ex.GetBaseException().Message);
                note.Add("(Warning: the sample attachments could not be archived.)");
                try { if (zipPath != null && File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                return null;
            }
        }

        private bool TrySecondaryCopy(string path, string name, string? zipName, List<string> note)
        {
            if (string.IsNullOrWhiteSpace(_opt.SecondaryDirectory)) return false;
            try
            {
                System.IO.Directory.CreateDirectory(_opt.SecondaryDirectory!);
                File.Copy(path, Path.Combine(_opt.SecondaryDirectory!, name), true);
                if (zipName != null) File.Copy(Path.Combine(_opt.Directory, zipName), Path.Combine(_opt.SecondaryDirectory!, zipName), true);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning("Secondary backup copy failed: {Msg}", ex.GetBaseException().Message);
                note.Add("(Warning: copy to the secondary folder failed.)");
                return false;
            }
        }

        /// مسح النسخ القديمة: بس ملفاتنا (DB_*.bak)، وما بنقلّش عن KeepMinimumFiles أبداً.
        private void ApplyRetention(string dir, string dbName, DateTime nowUtc, List<string> note)
        {
            try
            {
                if (!System.IO.Directory.Exists(dir)) return;
                var cutoff = nowUtc.AddDays(-Math.Max(1, _opt.KeepDays));
                foreach (var pattern in new[] { $"{SafeFileName(dbName)}_*.bak", $"{SafeFileName(dbName)}_*_attachments.zip" })
                {
                    var files = new DirectoryInfo(dir).GetFiles(pattern).OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                    foreach (var f in files.Skip(Math.Max(1, _opt.KeepMinimumFiles)).Where(f => f.LastWriteTimeUtc < cutoff))
                        try { f.Delete(); } catch (Exception ex) { _log.LogWarning("Could not delete old backup {File}: {Msg}", f.Name, ex.Message); }
                }
            }
            catch (Exception ex) { _log.LogWarning("Backup retention skipped for {Dir}: {Msg}", dir, ex.Message); }
        }

        private static long? TryFileSize(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : null; } catch { return null; }
        }

        private static void TryDelete(string? path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string SafeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private DateTime ToLocal(DateTime utc)
        {
            foreach (var id in new[] { _opt.TimeZoneId, "Africa/Cairo", "Egypt Standard Time" })
                try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(id)); } catch { }
            return utc.AddHours(2);
        }

        private static string Friendly(Exception ex)
        {
            var m = ex.GetBaseException().Message;
            if (m.Contains("Operating system error 5", StringComparison.OrdinalIgnoreCase) || m.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
                m += " | Fix: give the SQL Server service account Modify permission on the backup folder.";
            else if (m.Contains("Operating system error 3", StringComparison.OrdinalIgnoreCase) || m.Contains("cannot find the path", StringComparison.OrdinalIgnoreCase))
                m += " | Fix: the folder/drive does not exist on the SQL Server machine.";
            else if (m.Contains("permission was denied", StringComparison.OrdinalIgnoreCase))
                m += " | Fix: the database login needs db_backupoperator (or db_owner) rights.";
            return Truncate(m, 900)!;
        }

        private static string? Truncate(string? s, int max) => s == null ? null : (s.Length <= max ? s : s.Substring(0, max));

        private static BackupLogDto Map(Bio_BackupLog l) => new()
        {
            Id = l.Id,
            StartedAt = l.StartedAt,
            Type = l.Type,
            Status = l.Status,
            FileName = l.FileName,
            SizeBytes = l.SizeBytes,
            DurationMs = l.DurationMs,
            Verified = l.Verified,
            AttachmentsFileName = l.AttachmentsFileName,
            Error = l.Error,
            TriggeredBy = l.TriggeredBy
        };
    }
}
