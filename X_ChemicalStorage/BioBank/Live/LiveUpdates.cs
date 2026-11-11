using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Live
{
    /// <summary>
    /// SignalR hub for live screens (Station, Bio Bank). Server → client only:
    ///   "freezersChanged" { freezerIds: string[], at }   a freezer's reading / status / alarms / link changed
    ///   "stationChanged"  { at }                          something only the station shows changed (unlinked collector…)
    ///   "maintenance"     { active, reason }              a restore is running / finished
    /// Clients re-read the same REST endpoints they already use, so nothing here duplicates business logic.
    /// </summary>
    [Authorize]
    public class LiveHub : Hub { }

    /// Coalesces bursts (one MQTT message can touch freezer + alarms + settings) into one push every ~400 ms.
    public sealed class LiveBroadcaster : IDisposable
    {
        private readonly IHubContext<LiveHub> _hub;
        private readonly ILogger<LiveBroadcaster> _log;
        private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);
        private int _stationPending;
        private readonly Timer _timer;
        private int _scheduled;

        public LiveBroadcaster(IHubContext<LiveHub> hub, ILogger<LiveBroadcaster> log)
        {
            _hub = hub; _log = log;
            _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void FreezersChanged(IEnumerable<string> ids)
        {
            var any = false;
            foreach (var id in ids)
                if (!string.IsNullOrWhiteSpace(id)) { _pending[id] = 0; any = true; }
            if (any) Schedule();
        }

        public void StationChanged()
        {
            Interlocked.Exchange(ref _stationPending, 1);
            Schedule();
        }

        public Task MaintenanceAsync(bool active, string? reason) =>
            SafeSend("maintenance", new { active, reason });

        private void Schedule()
        {
            if (Interlocked.Exchange(ref _scheduled, 1) == 0)
                _timer.Change(400, Timeout.Infinite);
        }

        private void Flush()
        {
            Interlocked.Exchange(ref _scheduled, 0);
            var ids = _pending.Keys.ToArray();
            foreach (var id in ids) _pending.TryRemove(id, out _);
            var station = Interlocked.Exchange(ref _stationPending, 0) == 1;

            var at = DateTime.UtcNow;
            if (ids.Length > 0) _ = SafeSend("freezersChanged", new { freezerIds = ids, at });
            else if (station) _ = SafeSend("stationChanged", new { at });
        }

        private async Task SafeSend(string method, object payload)
        {
            try { await _hub.Clients.All.SendAsync(method, payload); }
            catch (Exception ex) { _log.LogDebug("Live push {Method} failed: {Msg}", method, ex.Message); }   // screens still poll
        }

        public void Dispose() => _timer.Dispose();
    }

    /// Watches EF saves for anything a live screen shows, so every path (MQTT, watchdog, acknowledge, settings…)
    /// pushes an update without each caller having to remember to.
    public sealed class LiveChangeInterceptor : SaveChangesInterceptor
    {
        private readonly LiveBroadcaster _live;
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, HashSet<string>> _batch = new();

        public LiveChangeInterceptor(LiveBroadcaster live) => _live = live;

        private void Collect(DbContext? ctx)
        {
            if (ctx == null) return;
            HashSet<string>? ids = null;
            foreach (var e in ctx.ChangeTracker.Entries())
            {
                if (e.State is EntityState.Unchanged or EntityState.Detached) continue;
                var id = e.Entity switch
                {
                    Bio_Freezer f => f.Id,
                    Bio_FreezerAlarm a => a.FreezerId,
                    Bio_FreezerSettings s => s.FreezerId,
                    Bio_IoTDevice d => d.FreezerId,
                    _ => null
                };
                if (id != null) (ids ??= new HashSet<string>(StringComparer.Ordinal)).Add(id);
            }
            _batch.Remove(ctx);
            if (ids != null) _batch.Add(ctx, ids);
        }

        private void Publish(DbContext? ctx)
        {
            if (ctx == null || !_batch.TryGetValue(ctx, out var ids)) return;
            _batch.Remove(ctx);
            if (ids.Count > 0) _live.FreezersChanged(ids);
        }

        private void Drop(DbContext? ctx) { if (ctx != null) _batch.Remove(ctx); }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        { Collect(eventData.Context); return result; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        { Collect(eventData.Context); return ValueTask.FromResult(result); }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        { Publish(eventData.Context); return result; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        { Publish(eventData.Context); return ValueTask.FromResult(result); }

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => Drop(eventData.Context);

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
        { Drop(eventData.Context); return Task.CompletedTask; }
    }
}
