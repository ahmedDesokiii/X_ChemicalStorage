using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    /// بيبعت الإشعارات Pending. الفشل => إعادة محاولة بتأخير متزايد لحد MaxAttempts.
    public class NotificationDispatcher
    {
        private readonly ApplicationDbContext _db;
        private readonly IEnumerable<INotificationChannel> _channels;
        private readonly NotificationOptions _opt;
        private readonly ILogger<NotificationDispatcher> _log;

        public NotificationDispatcher(ApplicationDbContext db, IEnumerable<INotificationChannel> channels,
            IOptions<NotificationOptions> opt, ILogger<NotificationDispatcher> log)
        {
            _db = db;
            _channels = channels;
            _opt = opt.Value;
            _log = log;
        }

        public static TimeSpan Backoff(int attempts) => attempts switch
        {
            <= 1 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(1),
            3 => TimeSpan.FromMinutes(2),
            4 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(10)
        };

        public async Task<int> ProcessPendingAsync(DateTime now, CancellationToken ct = default)
        {
            var due = await _db.AlarmNotifications
                .Where(n => n.Status == Bio_NotificationStatus.Pending && n.NextAttemptAt <= now)
                .OrderBy(n => n.Id).Take(20).ToListAsync(ct);

            foreach (var n in due)
            {
                if ((now - n.CreatedAt).TotalHours > _opt.ExpireAfterHours)
                {
                    n.Status = Bio_NotificationStatus.Failed;
                    n.Error = "Expired before it could be delivered";
                    continue;
                }

                var ch = _channels.FirstOrDefault(c => c.Name == n.Channel);
                if (ch == null || !ch.IsEnabled)
                {
                    n.Status = Bio_NotificationStatus.Skipped;
                    n.Error = "Channel is not enabled";
                    continue;
                }

                n.Attempts++;
                try
                {
                    await ch.SendAsync(n.Recipient, n.Subject, n.Message, ct);
                    n.Status = Bio_NotificationStatus.Sent;
                    n.SentAt = now;
                    n.Error = null;
                }
                catch (Exception ex)
                {
                    n.Error = Str.Trunc(ex.GetBaseException().Message, 500);
                    if (n.Attempts >= _opt.MaxAttempts)
                    {
                        n.Status = Bio_NotificationStatus.Failed;
                        _log.LogError("Notification #{Id} to {To} via {Ch} FAILED permanently: {Err}", n.Id, n.Recipient, n.Channel, n.Error);
                    }
                    else
                    {
                        n.NextAttemptAt = now + Backoff(n.Attempts);
                        _log.LogWarning("Notification #{Id} via {Ch} failed (attempt {A}), retry at {At:u}: {Err}",
                            n.Id, n.Channel, n.Attempts, n.NextAttemptAt, n.Error);
                    }
                }
            }

            if (due.Count > 0) await _db.SaveChangesAsync(ct);
            return due.Count;
        }
    }

    public class AlarmNotificationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly NotificationOptions _opt;
        private readonly ILogger<AlarmNotificationWorker> _log;

        public AlarmNotificationWorker(IServiceScopeFactory scopes, IOptions<NotificationOptions> opt,
            ILogger<AlarmNotificationWorker> log)
        {
            _scopes = scopes;
            _opt = opt.Value;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch (OperationCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var d = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                    await d.ProcessPendingAsync(DateTime.UtcNow, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "Notification worker cycle failed"); }

                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, _opt.WorkerIntervalSeconds)), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}