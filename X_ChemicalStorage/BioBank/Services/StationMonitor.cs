using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace X_ChemicalStorage.BioBank.Services
{
    /// A collector that sends data on the local network but is not linked to any freezer yet.
    public record CollectorSighting(string CollectorCode, string Topic, DateTime FirstSeenAt, DateTime LastSeenAt, decimal? Temperature, long Messages);

    /// <summary>
    /// Live state of the local monitoring station (the PC that runs the broker and this app on the LAN):
    /// collectors heard but not linked, and whether the internet is reachable (Telegram/SMS/e-mail need it,
    /// the on-screen station alarm does not).
    /// </summary>
    public class StationState
    {
        private readonly ConcurrentDictionary<string, CollectorSighting> _unlinked = new(StringComparer.OrdinalIgnoreCase);
        private int _internetState;   // 0 = not checked yet, 1 = online, 2 = offline
        private long _internetCheckedTicks;

        public void UnlinkedCollectorSeen(string collector, string topic, decimal? temperature, DateTime now) =>
            _unlinked.AddOrUpdate(collector,
                _ => new CollectorSighting(collector, topic, now, now, temperature, 1),
                (_, old) => old with { Topic = topic, LastSeenAt = now, Temperature = temperature ?? old.Temperature, Messages = old.Messages + 1 });

        public void CollectorLinked(string collector) => _unlinked.TryRemove(collector, out _);

        /// Collectors heard during the last 24 hours that are still not linked.
        public IReadOnlyList<CollectorSighting> UnlinkedCollectors(DateTime now, ISet<string>? linkedCodes = null)
        {
            foreach (var old in _unlinked.Values.Where(c => (now - c.LastSeenAt).TotalHours > 24).ToList())
                _unlinked.TryRemove(old.CollectorCode, out _);

            return _unlinked.Values
                .Where(c => linkedCodes == null || !linkedCodes.Contains(c.CollectorCode))
                .OrderByDescending(c => c.LastSeenAt)
                .ToList();
        }

        public bool? InternetOnline => Volatile.Read(ref _internetState) switch { 1 => true, 2 => false, _ => null };
        public DateTime? InternetCheckedUtc => Interlocked.Read(ref _internetCheckedTicks) is var t && t != 0 ? new DateTime(t, DateTimeKind.Utc) : null;

        public void SetInternet(bool online, DateTime now)
        {
            Volatile.Write(ref _internetState, online ? 1 : 2);
            Interlocked.Exchange(ref _internetCheckedTicks, now.Ticks);
        }

        /// IPv4 addresses of this machine on the local network (what the gateways should point at).
        public static List<string> LocalAddresses()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }

    /// Checks every minute whether the internet is reachable, so the station can say
    /// "remote notifications are down — only the local alarm is active".
    public class ConnectivityMonitorService : BackgroundService
    {
        private readonly IHttpClientFactory _http;
        private readonly StationState _state;
        private readonly MonitoringOptions _opt;
        private readonly ILogger<ConnectivityMonitorService> _log;

        public ConnectivityMonitorService(IHttpClientFactory http, StationState state, IOptions<MonitoringOptions> opt, ILogger<ConnectivityMonitorService> log)
        {
            _http = http; _state = state; _opt = opt.Value; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_opt.InternetProbeUrl)) return;
            var interval = TimeSpan.FromSeconds(Math.Max(15, _opt.InternetCheckSeconds));

            while (!ct.IsCancellationRequested)
            {
                var online = await ProbeAsync(ct);
                if (_state.InternetOnline != online)
                    _log.Log(online ? LogLevel.Information : LogLevel.Warning,
                        online ? "Internet is reachable again." : "Internet is NOT reachable: remote alarm notifications are paused, the local station alarm stays active.");
                _state.SetInternet(online, DateTime.UtcNow);

                try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
            }
        }

        private async Task<bool> ProbeAsync(CancellationToken ct)
        {
            try
            {
                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(6);
                using var req = new HttpRequestMessage(HttpMethod.Head, _opt.InternetProbeUrl);
                using var resp = await client.SendAsync(req, ct);
                return true;   // any HTTP answer means the internet path works
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }
    }
}
