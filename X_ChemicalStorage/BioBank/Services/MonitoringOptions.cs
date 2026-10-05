using Microsoft.Extensions.Options;

namespace X_ChemicalStorage.BioBank.Services
{
    public class MonitoringOptions
    {
        public const string SectionName = "Monitoring";

        /// رابط خدمة مراقبة خارجية (healthchecks.io / Uptime Kuma / ...). فاضي = معطّل.
        public string HeartbeatUrl { get; set; } = "";
        public int HeartbeatIntervalMinutes { get; set; } = 5;
    }

    public record HealthReport(string Status, List<string> Problems, List<string> Warnings);

    /// حالة سلامة نظام المراقبة نفسه (مين بيراقب المراقِب؟).
    public class IoTHealth
    {
        private long _watchdogTicks, _notifTicks, _mqttMsgTicks;
        private volatile bool _mqttConnected;

        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public bool MqttEnabled { get; set; }
        public bool MqttConnected { get => _mqttConnected; set => _mqttConnected = value; }

        public void WatchdogRan(DateTime t) => Interlocked.Exchange(ref _watchdogTicks, t.Ticks);
        public void NotificationWorkerRan(DateTime t) => Interlocked.Exchange(ref _notifTicks, t.Ticks);
        public void MqttMessageReceived(DateTime t) => Interlocked.Exchange(ref _mqttMsgTicks, t.Ticks);

        public DateTime? LastWatchdogRunUtc => Get(ref _watchdogTicks);
        public DateTime? LastNotificationWorkerRunUtc => Get(ref _notifTicks);
        public DateTime? LastMqttMessageUtc => Get(ref _mqttMsgTicks);

        private static DateTime? Get(ref long ticks)
        {
            var v = Interlocked.Read(ref ticks);
            return v == 0 ? null : new DateTime(v, DateTimeKind.Utc);
        }

        /// down = النظام نفسه مش شغال صح (مفيش إنذارات هتتبعت) | degraded = شغال بس فيه ملاحظة | ok
        public HealthReport Evaluate(DateTime now)
        {
            var problems = new List<string>();
            var warnings = new List<string>();
            var up = now - StartedUtc;

            if (up >= TimeSpan.FromMinutes(2))   // مهلة بعد التشغيل
            {
                if (Stale(LastWatchdogRunUtc, now, 3)) problems.Add("Alarm watchdog is not running (offline detection and escalation are down).");
                if (Stale(LastNotificationWorkerRunUtc, now, 3)) problems.Add("Notification worker is not running (alarm messages are not being sent).");
                if (MqttEnabled && !MqttConnected) problems.Add("MQTT broker is not connected (no freezer data is being received).");
            }

            if (MqttEnabled && MqttConnected && up >= TimeSpan.FromMinutes(5) && Stale(LastMqttMessageUtc, now, 5))
                warnings.Add("Connected to the broker but no freezer message received for 5+ minutes.");

            var status = problems.Count > 0 ? "down" : warnings.Count > 0 ? "degraded" : "ok";
            return new HealthReport(status, problems, warnings);
        }

        private static bool Stale(DateTime? t, DateTime now, int minutes) => t == null || (now - t.Value).TotalMinutes > minutes;
    }

    /// Dead-man's switch: بيبعت Ping لخدمة خارجية كل 5 دقايق طول ما النظام سليم.
    /// لو الـ Pings وقفت (السيرفر وقع / النت قطع / المحرك واقف) الخدمة الخارجية هي اللي تنبّهك.
    public class HeartbeatService : BackgroundService
    {
        private readonly IHttpClientFactory _http;
        private readonly MonitoringOptions _opt;
        private readonly IoTHealth _health;
        private readonly ILogger<HeartbeatService> _log;

        public HeartbeatService(IHttpClientFactory http, IOptions<MonitoringOptions> opt, IoTHealth health, ILogger<HeartbeatService> log)
        {
            _http = http; _opt = opt.Value; _health = health; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_opt.HeartbeatUrl))
            {
                _log.LogInformation("Heartbeat monitoring is disabled (Monitoring:HeartbeatUrl is empty).");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _opt.HeartbeatIntervalMinutes));
            while (!ct.IsCancellationRequested)
            {
                await TickAsync(DateTime.UtcNow, ct);
                try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
            }
        }

        public async Task<bool> TickAsync(DateTime now, CancellationToken ct = default)
        {
            var r = _health.Evaluate(now);
            if (r.Status == "down")
            {
                _log.LogError("Heartbeat NOT sent - system is down: {Problems}", string.Join(" | ", r.Problems));
                return false;
            }

            try
            {
                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                using var resp = await client.GetAsync(_opt.HeartbeatUrl, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("Heartbeat ping failed: HTTP {Code}", (int)resp.StatusCode);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning("Heartbeat ping failed: {Msg}", ex.GetBaseException().Message);
                return false;
            }
        }
    }
}