
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;
namespace X_ChemicalStorage.BioBank.Services
{
    public class IoTDashboardService : IIoTDashboardService
    {
        private readonly ApplicationDbContext _db;
        private readonly NotificationOptions _opt;

        public IoTDashboardService(ApplicationDbContext db, IOptions<NotificationOptions> opt)
        {
            _db = db;
            _opt = opt.Value;
        }

        // ============================================================ Dashboard
        public async Task<IoTDashboardDto> GetDashboardAsync(int days, DateTime? nowUtc = null)
        {
            days = Math.Clamp(days, 1, 90);
            var now = nowUtc ?? DateTime.UtcNow;
            var tz = ResolveTimeZone();

            var activeDevices = await _db.Bio_IoTDevices.AsNoTracking().Where(d => d.IsActive).ToListAsync();
            var monitoredIds = activeDevices.Select(d => d.FreezerId).Distinct().ToList();
            var collectorBy = activeDevices.GroupBy(d => d.FreezerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreatedAt).First().CollectorCode);

            var freezers = (await _db.Bio_Freezers.AsNoTracking().ToListAsync())
                .Where(f => monitoredIds.Contains(f.Id)).OrderBy(f => f.Id).ToList();

            var settings = (await _db.Bio_FreezerSettings.AsNoTracking().ToListAsync())
                .ToDictionary(s => s.FreezerId);

            var open = await _db.Bio_FreezerAlarms.AsNoTracking()
                .Where(a => !a.IsCleared || a.AcknowledgedAt == null).ToListAsync();

            // ----- حالة كل فريزر -----
            var dto = new IoTDashboardDto { Days = days };
            foreach (var f in freezers)
            {
                settings.TryGetValue(f.Id, out var s);
                var mine = open.Where(a => a.FreezerId == f.Id).ToList();
                var active = mine.Where(a => !a.IsCleared).Select(a => a.AlarmType).Distinct().ToList();

                bool commissioning = s == null || s.IsCommissioning;
                string state;
                if (commissioning) state = IoTState.Commissioning;
                else if (f.Status == FreezerStatus.Offline || active.Contains(Bio_AlarmTypes.Offline)) state = IoTState.Offline;
                else if (active.Contains(Bio_AlarmTypes.HighTemp)) state = IoTState.HighTemp;
                else if (active.Contains(Bio_AlarmTypes.LowTemp)) state = IoTState.LowTemp;
                else if (active.Count > 0) state = IoTState.Abnormal;
                else state = IoTState.Normal;

                var def = Bio_FreezerSettings.CreateDefault(f.Id, f.Type);
                dto.Devices.Add(new IoTDeviceStateDto
                {
                    FreezerId = f.Id,
                    Type = f.Type.ToString(),
                    CollectorCode = collectorBy.TryGetValue(f.Id, out var cc) ? cc : null,
                    State = state,
                    // لو الفريزر عمره ما بعت قراءة، CurrentTemperature قيمة افتراضية مش حقيقية
                    Temperature = f.LastReadingAt.HasValue ? f.CurrentTemperature : null,
                    LastReadingAt = f.LastReadingAt,
                    HighTempLimit = s?.HighTempLimit ?? def.HighTempLimit,
                    LowTempLimit = s?.LowTempLimit ?? def.LowTempLimit,
                    UnacknowledgedAlarms = mine.Count(a => a.AcknowledgedAt == null),
                    ActiveAlarmTypes = active
                });
            }

            var c = dto.Counts;
            c.All = dto.Devices.Count;
            c.Normal = dto.Devices.Count(d => d.State == IoTState.Normal);
            c.HighTemp = dto.Devices.Count(d => d.State == IoTState.HighTemp);
            c.LowTemp = dto.Devices.Count(d => d.State == IoTState.LowTemp);
            c.Abnormal = dto.Devices.Count(d => d.State == IoTState.Abnormal);
            c.Offline = dto.Devices.Count(d => d.State == IoTState.Offline);
            c.Commissioning = dto.Devices.Count(d => d.State == IoTState.Commissioning);

            // ----- اتجاه الإنذارات (آخر N يوم، بالتاريخ المحلي) -----
            var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(now, tz).Date;
            var firstLocal = todayLocal.AddDays(-(days - 1));
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(firstLocal, DateTimeKind.Unspecified), tz);

            var period = (await _db.Bio_FreezerAlarms.AsNoTracking()
                .Where(a => a.TriggeredAt >= fromUtc).ToListAsync())
                .Where(a => monitoredIds.Contains(a.FreezerId)).ToList();

            for (int i = 0; i < days; i++)
            {
                var day = firstLocal.AddDays(i);
                var inDay = period.Where(a => TimeZoneInfo.ConvertTimeFromUtc(a.TriggeredAt, tz).Date == day).ToList();
                dto.Trend.Add(new IoTTrendPointDto
                {
                    Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    HighTemp = inDay.Count(a => a.AlarmType == Bio_AlarmTypes.HighTemp),
                    LowTemp = inDay.Count(a => a.AlarmType == Bio_AlarmTypes.LowTemp),
                    Other = inDay.Count(a => a.AlarmType != Bio_AlarmTypes.HighTemp && a.AlarmType != Bio_AlarmTypes.LowTemp)
                });
            }

            // ----- أكتر 5 فريزرات إنذاراً -----
            dto.TopDevices = period.GroupBy(a => a.FreezerId)
                .Select(g => new IoTTopDeviceDto
                {
                    FreezerId = g.Key,
                    HighTemp = g.Count(a => a.AlarmType == Bio_AlarmTypes.HighTemp),
                    LowTemp = g.Count(a => a.AlarmType == Bio_AlarmTypes.LowTemp),
                    Other = g.Count(a => a.AlarmType != Bio_AlarmTypes.HighTemp && a.AlarmType != Bio_AlarmTypes.LowTemp),
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total).ThenBy(x => x.FreezerId).Take(5).ToList();

            return dto;
        }

        // ============================================================ Settings
        public async Task<FreezerSettingsDto?> GetSettingsAsync(string freezerId)
        {
            var freezer = await _db.Bio_Freezers.AsNoTracking().FirstOrDefaultAsync(f => f.Id == freezerId);
            if (freezer == null) return null;

            var s = await _db.Bio_FreezerSettings.AsNoTracking().FirstOrDefaultAsync(x => x.FreezerId == freezerId)
                    ?? Bio_FreezerSettings.CreateDefault(freezerId, freezer.Type);
            return Map(s);
        }

        public async Task<FreezerSettingsDto> UpdateSettingsAsync(string freezerId, UpdateFreezerSettingsDto dto, string performedBy)
        {
            var freezer = await _db.Bio_Freezers.FirstOrDefaultAsync(f => f.Id == freezerId)
                ?? throw new KeyNotFoundException($"Freezer '{freezerId}' not found.");

            var high = dto.HighTempLimit ?? throw new ArgumentException("HighTempLimit is required.");
            var low = dto.LowTempLimit ?? throw new ArgumentException("LowTempLimit is required.");
            var delay = dto.TempAlarmDelayMinutes ?? throw new ArgumentException("TempAlarmDelayMinutes is required.");
            var door = dto.DoorOpenMinutes ?? throw new ArgumentException("DoorOpenMinutes is required.");
            var offline = dto.OfflineAfterMinutes ?? throw new ArgumentException("OfflineAfterMinutes is required.");
            var esc = dto.EscalationMinutes ?? throw new ArgumentException("EscalationMinutes is required.");
            var commissioning = dto.IsCommissioning ?? throw new ArgumentException("IsCommissioning is required.");

            if (low >= high) throw new ArgumentException("Low limit must be lower than the high limit.");
            if (high > 50 || low < -250) throw new ArgumentException("Limits must be between -250 and 50 °C.");
            if (delay < 0 || delay > 120) throw new ArgumentException("Temperature alarm delay must be 0-120 minutes.");
            if (door < 1 || door > 120) throw new ArgumentException("Door-open time must be 1-120 minutes.");
            if (offline < 1 || offline > 1440) throw new ArgumentException("Offline timeout must be 1-1440 minutes.");
            if (esc < 1 || esc > 1440) throw new ArgumentException("Escalation time must be 1-1440 minutes.");

            var now = DateTime.UtcNow;
            var s = await _db.Bio_FreezerSettings.FirstOrDefaultAsync(x => x.FreezerId == freezerId);
            if (s == null)
            {
                s = Bio_FreezerSettings.CreateDefault(freezerId, freezer.Type);
                _db.Bio_FreezerSettings.Add(s);
            }

            var changes = new List<(string Field, string Old, string New)>();
            void Track<T>(string field, T oldV, T newV)
            {
                var o = Convert.ToString(oldV, CultureInfo.InvariantCulture) ?? "";
                var n = Convert.ToString(newV, CultureInfo.InvariantCulture) ?? "";
                if (o != n) changes.Add((field, o, n));
            }
            Track("HighTempLimit", s.HighTempLimit, high);
            Track("LowTempLimit", s.LowTempLimit, low);
            Track("TempAlarmDelayMinutes", s.TempAlarmDelayMinutes, delay);
            Track("DoorOpenMinutes", s.DoorOpenMinutes, door);
            Track("OfflineAfterMinutes", s.OfflineAfterMinutes, offline);
            Track("EscalationMinutes", s.EscalationMinutes, esc);
            Track("IsCommissioning", s.IsCommissioning, commissioning);

            s.HighTempLimit = high;
            s.LowTempLimit = low;
            s.TempAlarmDelayMinutes = delay;
            s.DoorOpenMinutes = door;
            s.OfflineAfterMinutes = offline;
            s.EscalationMinutes = esc;

            if (s.IsCommissioning != commissioning)
            {
                s.IsCommissioning = commissioning;
                if (commissioning)
                {
                    s.CommissioningEndedAt = null;
                    freezer.Status = FreezerStatus.Maintenance;       // مش بيقبل عينات لحد ما يرجع للنطاق
                }
                else
                {
                    s.CommissioningEndedAt = now;                     // إنهاء يدوي: مسجّل باسم المستخدم
                    if (freezer.Status == FreezerStatus.Maintenance) freezer.Status = FreezerStatus.Online;
                }
            }

            if (changes.Count > 0)
            {
                s.UpdatedAt = now;
                s.UpdatedBy = performedBy;
                foreach (var ch in changes)
                    _db.FreezerSettingsAudit.Add(new Bio_FreezerSettingsAudit
                    {
                        FreezerId = freezerId,
                        ChangedAt = now,
                        ChangedBy = performedBy,
                        Field = ch.Field,
                        OldValue = ch.Old,
                        NewValue = ch.New
                    });
            }

            await _db.SaveChangesAsync();
            return Map(s);
        }

        public async Task<List<SettingsAuditDto>> GetSettingsAuditAsync(string freezerId, int take = 100)
        {
            take = Math.Clamp(take, 1, 500);
            return await _db.FreezerSettingsAudit.AsNoTracking()
                .Where(a => a.FreezerId == freezerId)
                .OrderByDescending(a => a.Id).Take(take)
                .Select(a => new SettingsAuditDto
                {
                    ChangedAt = a.ChangedAt,
                    ChangedBy = a.ChangedBy,
                    Field = a.Field,
                    OldValue = a.OldValue,
                    NewValue = a.NewValue
                }).ToListAsync();
        }

        // ============================================================ helpers
        private static FreezerSettingsDto Map(Bio_FreezerSettings s) => new()
        {
            FreezerId = s.FreezerId,
            HighTempLimit = s.HighTempLimit,
            LowTempLimit = s.LowTempLimit,
            TempAlarmDelayMinutes = s.TempAlarmDelayMinutes,
            DoorOpenMinutes = s.DoorOpenMinutes,
            OfflineAfterMinutes = s.OfflineAfterMinutes,
            EscalationMinutes = s.EscalationMinutes,
            IsCommissioning = s.IsCommissioning,
            CommissioningEndedAt = s.CommissioningEndedAt,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy
        };

        private TimeZoneInfo ResolveTimeZone()
        {
            foreach (var id in new[] { _opt.TimeZoneId, "Africa/Cairo", "Egypt Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("EG", TimeSpan.FromHours(2), "EG", "EG");
        }
    }
}
