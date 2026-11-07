using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.BioBank.Services;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
    [Route("biobank/api/iot")]
    public class IoTHealthController : Controller
    {
        private readonly IoTHealth _health;
        private readonly ApplicationDbContext _db;

        public IoTHealthController(IoTHealth health, ApplicationDbContext db)
        {
            _health = health;
            _db = db;
        }

        /// سلامة نظام المراقبة نفسه + آخر مرة كل Gateway بعت بيانات + طابور الإشعارات.
        [HasPermission(Permissions.IoT.View)]
        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            var now = DateTime.UtcNow;
            var r = _health.Evaluate(now);

            var pending = await _db.AlarmNotifications.AsNoTracking()
                .Where(n => n.Status == Bio_NotificationStatus.Pending).ToListAsync();
            var since = now.AddHours(-24);
            var failed24 = (await _db.AlarmNotifications.AsNoTracking()
                .Where(n => n.Status == Bio_NotificationStatus.Failed && n.CreatedAt >= since).ToListAsync()).Count;

            var warnings = new List<string>(r.Warnings);
            var oldest = pending.Count == 0 ? (double?)null : pending.Min(n => (now - n.CreatedAt).TotalMinutes);
            if (oldest > 10) warnings.Add($"Oldest pending notification is {Math.Round(oldest.Value)} min old (provider/internet problem?).");
            if (failed24 > 0) warnings.Add($"{failed24} notification(s) failed permanently in the last 24h.");

            var monitored = (await _db.Bio_IoTDevices.AsNoTracking().Where(d => d.IsActive).ToListAsync())
                .Select(d => d.FreezerId).Distinct().ToList();
            var gateways = (await _db.Bio_Freezers.AsNoTracking().ToListAsync())
                .Where(f => monitored.Contains(f.Id)).OrderBy(f => f.Id)
                .Select(f => new
                {
                    freezerId = f.Id,
                    lastReadingAt = f.LastReadingAt,
                    minutesSinceData = f.LastReadingAt.HasValue ? Math.Round((now - f.LastReadingAt.Value).TotalMinutes, 1) : (double?)null
                }).ToList();

            var status = r.Status == "ok" && warnings.Count > 0 ? "degraded" : r.Status;
            return Ok(new
            {
                status,
                problems = r.Problems,
                warnings,
                uptimeMinutes = Math.Round((now - _health.StartedUtc).TotalMinutes, 1),
                mqtt = new { enabled = _health.MqttEnabled, connected = _health.MqttConnected, lastMessageAt = _health.LastMqttMessageUtc },
                watchdogLastRunAt = _health.LastWatchdogRunUtc,
                notificationWorkerLastRunAt = _health.LastNotificationWorkerRunUtc,
                notifications = new { pending = pending.Count, oldestPendingMinutes = oldest.HasValue ? Math.Round(oldest.Value, 1) : (double?)null, failedLast24h = failed24 },
                gateways
            });
        }
    }
}