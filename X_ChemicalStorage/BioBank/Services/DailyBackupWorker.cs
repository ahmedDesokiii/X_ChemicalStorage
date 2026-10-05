using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;

namespace X_ChemicalStorage.BioBank.Services
{
    /// بيشيّك كل دقيقة: لو النسخة اليومية مستحقة (الموعد جه ومفيش نسخة ناجحة بعده) بياخدها.
    /// لو التطبيق كان واقف وقت الموعد، بياخدها أول ما يشتغل.
    public class DailyBackupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly BackupOptions _opt;
        private readonly ILogger<DailyBackupWorker> _log;

        public DailyBackupWorker(IServiceScopeFactory scopes, IOptions<BackupOptions> opt, ILogger<DailyBackupWorker> log)
        {
            _scopes = scopes; _opt = opt.Value; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (!_opt.Enabled)
            {
                _log.LogInformation("Automatic database backup is disabled (Backup:Enabled=false).");
                return;
            }
            _log.LogInformation("Automatic database backup: daily at {Time} ({Tz}) -> {Dir}", _opt.DailyTime, _opt.TimeZoneId, _opt.Directory);

            try { await Task.Delay(TimeSpan.FromSeconds(45), ct); } catch (OperationCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                try { await CheckAsync(DateTime.UtcNow, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "Backup scheduler cycle failed"); }

                try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch (OperationCanceledException) { break; }
            }
        }

        public async Task<bool> CheckAsync(DateTime nowUtc, CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var tz = ResolveTz();
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var since = nowUtc.AddDays(-3);
            var attempts = (await db.BackupLogs.AsNoTracking().Where(l => l.Type == "Auto" && l.StartedAt >= since).ToListAsync(ct))
                .Select(l => (TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(l.StartedAt, DateTimeKind.Utc), tz), l.Status == "Success"))
                .ToList();

            if (!BackupScheduler.IsDue(localNow, BackupScheduler.ParseDailyTime(_opt.DailyTime), attempts, _opt.MaxAutoAttemptsPerDay, _opt.RetryAfterMinutes))
                return false;

            var svc = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var r = await svc.RunAsync("Auto", "system", ct);
            if (r.Success) _log.LogInformation("Automatic backup OK: {File} ({Size} bytes)", r.FileName, r.SizeBytes);
            else _log.LogError("Automatic backup did not complete: {Code} - {Msg}", r.Code, r.Message);
            return true;
        }

        private TimeZoneInfo ResolveTz()
        {
            foreach (var id in new[] { _opt.TimeZoneId, "Africa/Cairo", "Egypt Standard Time" })
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
            return TimeZoneInfo.CreateCustomTimeZone("EG", TimeSpan.FromHours(2), "EG", "EG");
        }
    }
}