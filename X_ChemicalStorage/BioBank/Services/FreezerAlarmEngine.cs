using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.BioBank.DTOs;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    /// محرك الإنذارات.
    /// - قيد التشغيل (IsCommissioning): لا إنذارات، والفريزر Maintenance (مش بيقبل عينات)، وبينتهي أول قراءة داخل النطاق.
    /// - الحرارة: خارج الحد لمدة TempAlarmDelayMinutes => إنذار. الرجوع للنطاق => إغلاق تلقائي.
    /// - الباب: مفتوح أكتر من DoorOpenMinutes. الكهرباء: فوري. Sensor: قراءة غير صالحة دقيقتين.
    /// - Offline: انقطاع الرسايل OfflineAfterMinutes (من الـ Watchdog).
    /// - التصعيد: إنذار بدون استلام EscalationMinutes => مستوى 2.
    /// - الاستلام (Acknowledge) لا يغلق الإنذار؛ الإغلاق بيحصل فقط لما الحالة ترجع طبيعية.
    public class FreezerAlarmEngine : IFreezerAlarmEngine
    {
        private const int SensorErrorMinutes = 2;

        // بداية كل حالة "خارج الطبيعي" (في الذاكرة). لو التطبيق اتعمله Restart، مهلة التأخير بتبدأ من جديد.
        private static readonly ConcurrentDictionary<(string Freezer, string Type), DateTime> _since = new();

        private readonly ApplicationDbContext _db;
        private readonly IAlarmNotifier _notifier;
        private readonly ILogger<FreezerAlarmEngine> _logger;
        private readonly ReminderOptions _reminder;
        private readonly List<(AlarmEventKind Kind, Bio_FreezerAlarm Alarm)> _pending = new();

        public FreezerAlarmEngine(ApplicationDbContext db, IAlarmNotifier notifier, ILogger<FreezerAlarmEngine> logger,
            IOptions<NotificationOptions> options)
        {
            _db = db;
            _notifier = notifier;
            _logger = logger;
            _reminder = options.Value.Reminder;
        }

        // ============================================================ قراءة واردة
        public async Task EvaluateReadingAsync(Bio_Freezer freezer, IngestReadingDto dto, DateTime now)
        {
            var s = await GetSettingsAsync(freezer, now);
            var open = await OpenAlarmsAsync(freezer.Id);

            if (dto.IsOnline)
                ClearOpen(open, Bio_AlarmTypes.Offline, now);

            // ----- قيد التشغيل -----
            if (s.IsCommissioning && dto.IsOnline && dto.Temperature is decimal ct
                && ct >= s.LowTempLimit && ct <= s.HighTempLimit)
            {
                s.IsCommissioning = false;
                s.CommissioningEndedAt = now;
                s.UpdatedAt = now;
                s.UpdatedBy = "system";
                if (freezer.Status == FreezerStatus.Maintenance)
                    freezer.Status = FreezerStatus.Online;
                _logger.LogInformation("Freezer {Id} reached normal range ({T}°C): commissioning ended, alarms enabled.",
                    freezer.Id, ct);
            }

            if (s.IsCommissioning)
            {
                freezer.Status = FreezerStatus.Maintenance;
                ClearTimers(freezer.Id);
                return;
            }

            // صيانة يدوية (مش قيد التشغيل): مفيش إنذارات جديدة ولا تغيير للحالة
            bool maintenance = freezer.Status == FreezerStatus.Maintenance;

            if (!dto.IsOnline)
            {
                if (!maintenance)
                {
                    Raise(freezer, open, Bio_AlarmTypes.Offline, 1, "Gateway reported offline. Check freezer power, gateway and network.", null, now);
                    freezer.Status = FreezerStatus.Offline;
                }
                return;
            }

            if (!maintenance)
            {
                EvaluateTemperature(freezer, s, open, dto, now);
                EvaluateDoor(freezer, s, open, dto, now);
                EvaluatePower(freezer, open, dto, now);
            }

            RefreshStatus(freezer, open);
        }

        private void EvaluateTemperature(Bio_Freezer f, Bio_FreezerSettings s, List<Bio_FreezerAlarm> open,
            IngestReadingDto dto, DateTime now)
        {
            if (dto.TemperatureInvalid)
            {
                if (Sustained(f.Id, Bio_AlarmTypes.SensorError, true, now, SensorErrorMinutes))
                    Raise(f, open, Bio_AlarmTypes.SensorError, 1,
                        "Invalid temperature reading (sensor error?). Payload: " + Trunc(dto.RawPayload, 200), null, now);
                return;
            }

            if (dto.Temperature is not decimal t) return;   // رسالة بدون حرارة: مفيش تقييم

            Sustained(f.Id, Bio_AlarmTypes.SensorError, false, now, 0);
            ClearOpen(open, Bio_AlarmTypes.SensorError, now);

            bool high = t > s.HighTempLimit;
            bool low = t < s.LowTempLimit;

            if (Sustained(f.Id, Bio_AlarmTypes.HighTemp, high, now, s.TempAlarmDelayMinutes))
                Raise(f, open, Bio_AlarmTypes.HighTemp, 1,
                    $"Temperature {t:F1}°C above limit {s.HighTempLimit:F0}°C for {s.TempAlarmDelayMinutes} min", t, now);
            else if (!high)
                ClearOpen(open, Bio_AlarmTypes.HighTemp, now);

            if (Sustained(f.Id, Bio_AlarmTypes.LowTemp, low, now, s.TempAlarmDelayMinutes))
                Raise(f, open, Bio_AlarmTypes.LowTemp, 1,
                    $"Temperature {t:F1}°C below limit {s.LowTempLimit:F0}°C for {s.TempAlarmDelayMinutes} min", t, now);
            else if (!low)
                ClearOpen(open, Bio_AlarmTypes.LowTemp, now);
        }

        private void EvaluateDoor(Bio_Freezer f, Bio_FreezerSettings s, List<Bio_FreezerAlarm> open,
            IngestReadingDto dto, DateTime now)
        {
            if (dto.DoorStatus == null) return;
            bool isOpen = dto.DoorStatus == "Open";

            if (Sustained(f.Id, Bio_AlarmTypes.DoorOpen, isOpen, now, s.DoorOpenMinutes))
                Raise(f, open, Bio_AlarmTypes.DoorOpen, 1, $"Door open for more than {s.DoorOpenMinutes} min", null, now);
            else if (!isOpen)
                ClearOpen(open, Bio_AlarmTypes.DoorOpen, now);
        }

        private void EvaluatePower(Bio_Freezer f, List<Bio_FreezerAlarm> open, IngestReadingDto dto, DateTime now)
        {
            if (dto.PowerStatus == null) return;

            if (dto.PowerStatus == "Fail")
                Raise(f, open, Bio_AlarmTypes.PowerLoss, 1, "Power failure / voltage fault reported by freezer", null, now);
            else
                ClearOpen(open, Bio_AlarmTypes.PowerLoss, now);
        }

        // ============================================================ فحوصات دورية
        public async Task RunPeriodicChecksAsync(DateTime now, CancellationToken ct = default)
        {
            var devices = await _db.Bio_IoTDevices.Where(d => d.IsActive).ToListAsync(ct);

            // فريزر واحد قد يكون عليه أكتر من جهاز نشط (تجريبي + حقيقي): نعالجه مرة واحدة بس
            foreach (var group in devices.GroupBy(d => d.FreezerId))
            {
                var freezer = await _db.Bio_Freezers.FirstOrDefaultAsync(f => f.Id == group.Key, ct);
                if (freezer == null) continue;
                var firstLinkedAt = group.Min(d => d.CreatedAt);

                var s = await GetSettingsAsync(freezer, now);
                var open = await OpenAlarmsAsync(freezer.Id);

                bool manualMaintenance = !s.IsCommissioning && freezer.Status == FreezerStatus.Maintenance;

                // ----- انقطاع الاتصال -----
                var last = freezer.LastReadingAt ?? firstLinkedAt;
                bool stale = (now - last).TotalMinutes >= s.OfflineAfterMinutes;

                if (stale && !manualMaintenance)
                {
                    freezer.Status = FreezerStatus.Offline;
                    ClearTimers(freezer.Id);   // بيانات قديمة: مهل التأخير تبدأ من جديد عند الرجوع
                    if (!s.IsCommissioning)
                        Raise(freezer, open, Bio_AlarmTypes.Offline, 1,
                            $"No data received for {s.OfflineAfterMinutes}+ min (last: {last:u}). Check freezer power, gateway and network.", null, now);
                }

                // ----- التصعيد -----
                foreach (var a in open.Where(a => !a.IsCleared && a.AcknowledgedAt == null && a.Level == 1
                                                  && (now - a.TriggeredAt).TotalMinutes >= s.EscalationMinutes))
                {
                    a.Level = 2;
                    a.EscalatedAt = now;
                    a.LastNotifiedAt = now;
                    _pending.Add((AlarmEventKind.Escalated, a));
                }

                // ----- التذكير المتكرر: لحد ما حد يستلم (أو الحالة ترجع طبيعية) -----
                if (_reminder.Enabled && !s.IsCommissioning && !manualMaintenance)
                {
                    var interval = Math.Max(1, _reminder.IntervalMinutes);
                    foreach (var a in open.Where(a => !a.IsCleared && a.AcknowledgedAt == null))
                    {
                        if (_reminder.MaxReminders > 0 && a.ReminderCount >= _reminder.MaxReminders) continue;
                        var lastSent = a.LastNotifiedAt ?? a.TriggeredAt;
                        if ((now - lastSent).TotalMinutes < interval) continue;

                        a.ReminderCount++;
                        a.LastNotifiedAt = now;
                        _pending.Add((AlarmEventKind.Reminder, a));
                    }
                }
            }

            await _db.SaveChangesAsync(ct);
            await FlushNotificationsAsync(ct);
        }

        // ============================================================ إشعارات
        public async Task FlushNotificationsAsync(CancellationToken ct = default)
        {
            if (_pending.Count == 0) return;
            var items = _pending.ToList();
            _pending.Clear();

            foreach (var (kind, a) in items)
            {
                try
                {
                    await _notifier.NotifyAsync(new AlarmNotification(
                        kind, a.Id, a.FreezerId, a.AlarmType, a.Level, a.Message, a.TriggeredAt, a.ReminderCount), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Alarm notification failed for alarm {Id}", a.Id);
                }
            }
        }

        // ============================================================ أدوات
        private async Task<Bio_FreezerSettings> GetSettingsAsync(Bio_Freezer freezer, DateTime now)
        {
            var s = await _db.Bio_FreezerSettings.FirstOrDefaultAsync(x => x.FreezerId == freezer.Id);
            if (s == null)
            {
                s = Bio_FreezerSettings.CreateDefault(freezer.Id, freezer.Type);
                s.UpdatedAt = now;
                _db.Bio_FreezerSettings.Add(s);
            }
            return s;
        }

        private Task<List<Bio_FreezerAlarm>> OpenAlarmsAsync(string freezerId) =>
            _db.Bio_FreezerAlarms.Where(a => a.FreezerId == freezerId && !a.IsCleared).ToListAsync();

        private bool Sustained(string freezerId, string type, bool condition, DateTime now, int minutes)
        {
            var key = (freezerId, type);
            if (!condition)
            {
                _since.TryRemove(key, out _);
                return false;
            }
            var since = _since.GetOrAdd(key, now);
            return (now - since).TotalMinutes >= minutes;
        }

        private static void ClearTimers(string freezerId)
        {
            foreach (var k in _since.Keys.Where(k => k.Freezer == freezerId).ToList())
                _since.TryRemove(k, out _);
        }

        private void Raise(Bio_Freezer f, List<Bio_FreezerAlarm> open, string type, int level,
            string message, decimal? value, DateTime now)
        {
            var existing = open.FirstOrDefault(a => a.AlarmType == type && !a.IsCleared);
            if (existing != null)
            {
                if (value.HasValue)
                {
                    if (type == Bio_AlarmTypes.HighTemp)
                        existing.PeakValue = existing.PeakValue.HasValue ? Math.Max(existing.PeakValue.Value, value.Value) : value;
                    else if (type == Bio_AlarmTypes.LowTemp)
                        existing.PeakValue = existing.PeakValue.HasValue ? Math.Min(existing.PeakValue.Value, value.Value) : value;
                }
                return;
            }

            var alarm = new Bio_FreezerAlarm
            {
                FreezerId = f.Id,
                AlarmType = type,
                Level = level,
                Message = Trunc(message, 500)!,
                TriggeredAt = now,
                LastNotifiedAt = now,
                PeakValue = value
            };
            _db.Bio_FreezerAlarms.Add(alarm);
            open.Add(alarm);
            _pending.Add((AlarmEventKind.Raised, alarm));
        }

        private void ClearOpen(List<Bio_FreezerAlarm> open, string type, DateTime now)
        {
            foreach (var a in open.Where(a => a.AlarmType == type && !a.IsCleared))
            {
                a.IsCleared = true;
                a.ClearedAt = now;
                _pending.Add((AlarmEventKind.Cleared, a));
            }
        }

        private static void RefreshStatus(Bio_Freezer f, List<Bio_FreezerAlarm> open)
        {
            if (f.Status == FreezerStatus.Maintenance) return;

            var active = open.Where(a => !a.IsCleared).ToList();
            if (active.Any(a => a.AlarmType != Bio_AlarmTypes.Offline)) f.Status = FreezerStatus.Alarm;
            else if (active.Any()) f.Status = FreezerStatus.Offline;
            else f.Status = FreezerStatus.Online;
        }

        private static string? Trunc(string? s, int max) =>
            s == null ? null : (s.Length <= max ? s : s.Substring(0, max));
    }
}