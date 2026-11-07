using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.BioBank.Services;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.BioBank.DTOs;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
    [Route("biobank/api/iot")]
    public class IoTHealthController : Controller
    {
        private readonly IoTHealth _health;
        private readonly ApplicationDbContext _db;
        private readonly StationState _station;
        private readonly IIoTDashboardService _dashboard;
        private readonly IFreezerIoTService _iot;
        private readonly X_ChemicalStorage.BioBank.Options.MqttOptions _mqtt;

        public IoTHealthController(IoTHealth health, ApplicationDbContext db, StationState station,
            IIoTDashboardService dashboard, IFreezerIoTService iot,
            Microsoft.Extensions.Options.IOptions<X_ChemicalStorage.BioBank.Options.MqttOptions> mqtt)
        {
            _health = health;
            _db = db;
            _station = station;
            _dashboard = dashboard;
            _iot = iot;
            _mqtt = mqtt.Value;
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

        /// <summary>
        /// Everything the local monitoring station screen needs in one call:
        /// station/broker/internet health, gateways with their freezers, unlinked freezers and collectors, open alarms.
        /// </summary>
        [HasPermission(Permissions.IoT.View)]
        [HttpGet("station")]
        public async Task<IActionResult> Station()
        {
            var now = DateTime.UtcNow;
            var health = _health.Evaluate(now);
            var problems = new List<string>(health.Problems);
            var warnings = new List<string>(health.Warnings);

            if (_station.InternetOnline == false)
                warnings.Add("No internet: Telegram / SMS / e-mail alarms are queued until it is back. The local station alarm is still active.");

            var pendingNotifications = await _db.AlarmNotifications.AsNoTracking()
                .CountAsync(n => n.Status == Bio_NotificationStatus.Pending);

            // Freezer states (same rules as the IoT dashboard)
            var dash = await _dashboard.GetDashboardAsync(1, now);
            var devices = await _db.Bio_IoTDevices.AsNoTracking().Where(d => d.IsActive).ToListAsync();
            var byFreezer = devices.GroupBy(d => d.FreezerId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Id).First());

            var gateways = dash.Devices
                .Where(d => byFreezer.ContainsKey(d.FreezerId))
                .GroupBy(d => byFreezer[d.FreezerId].GatewaySerial)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var freezers = g.OrderBy(d => d.FreezerId).Select(d => new
                    {
                        freezerId = d.FreezerId,
                        type = d.Type,
                        collectorCode = byFreezer[d.FreezerId].CollectorCode,
                        deviceName = byFreezer[d.FreezerId].DeviceName,
                        state = d.State,
                        temperature = d.Temperature,
                        highLimit = d.HighTempLimit,
                        lowLimit = d.LowTempLimit,
                        lastReadingAt = d.LastReadingAt,
                        minutesSinceData = d.LastReadingAt.HasValue ? Math.Round((now - d.LastReadingAt.Value).TotalMinutes, 1) : (double?)null,
                        unacknowledgedAlarms = d.UnacknowledgedAlarms,
                        activeAlarmTypes = d.ActiveAlarmTypes
                    }).ToList();

                    var offline = freezers.Count(f => f.state == IoTState.Offline);
                    return new
                    {
                        gatewaySerial = g.Key,
                        status = offline == 0 ? "online" : offline == freezers.Count ? "offline" : "partial",
                        freezerCount = freezers.Count,
                        onlineCount = freezers.Count - offline,
                        lastSeenAt = freezers.Max(f => f.lastReadingAt),
                        freezers
                    };
                }).ToList();

            foreach (var gw in gateways.Where(g => g.status == "offline"))
                problems.Add($"Gateway {gw.gatewaySerial} is offline: none of its {gw.freezerCount} freezer(s) is sending data. Check its power and network cable.");

            var linkedIds = byFreezer.Keys.ToHashSet();
            var unlinkedFreezers = await _db.Bio_Freezers.AsNoTracking()
                .Where(f => !linkedIds.Contains(f.Id))
                .OrderBy(f => f.Id)
                .Select(f => new { freezerId = f.Id, type = f.Type.ToString(), setPoint = f.Temperature })
                .ToListAsync();

            var linkedCodes = devices.Select(d => d.CollectorCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unlinkedCollectors = _station.UnlinkedCollectors(now, linkedCodes).Select(c => new
            {
                collectorCode = c.CollectorCode,
                topic = c.Topic,
                firstSeenAt = c.FirstSeenAt,
                lastSeenAt = c.LastSeenAt,
                temperature = c.Temperature,
                messages = c.Messages
            }).ToList();
            if (unlinkedCollectors.Count > 0)
                warnings.Add($"{unlinkedCollectors.Count} collector(s) are sending data but are not linked to a freezer.");

            var alarms = await _iot.GetActiveAlarmsAsync(null);

            var status = problems.Count > 0 ? "down" : warnings.Count > 0 ? "degraded" : "ok";
            return Ok(new
            {
                status,
                problems,
                warnings,
                serverTimeUtc = now,
                station = new
                {
                    machineName = Environment.MachineName,
                    addresses = StationState.LocalAddresses(),
                    uptimeMinutes = Math.Round((now - _health.StartedUtc).TotalMinutes, 1)
                },
                broker = new
                {
                    enabled = _health.MqttEnabled,
                    connected = _health.MqttConnected,
                    host = _mqtt.Host,
                    port = _mqtt.Port,
                    topic = _mqtt.Topic,
                    lastMessageAt = _health.LastMqttMessageUtc
                },
                internet = new { online = _station.InternetOnline, checkedAt = _station.InternetCheckedUtc },
                workers = new
                {
                    watchdogLastRunAt = _health.LastWatchdogRunUtc,
                    notificationWorkerLastRunAt = _health.LastNotificationWorkerRunUtc,
                    pendingNotifications
                },
                counts = dash.Counts,
                gateways,
                unlinkedFreezers,
                unlinkedCollectors,
                alarms
            });
        }
    }
}