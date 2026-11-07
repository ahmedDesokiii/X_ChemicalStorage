using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    public class FreezerIoTService : IFreezerIoTService
    {
        private readonly ApplicationDbContext _db;
        private readonly MqttOptions _mqtt;
        private readonly IFreezerAlarmEngine _alarms;

        // آخر قراءة اتخزنت لكل فريزر (لتطبيق فاصل التخزين)
        private static readonly ConcurrentDictionary<string, (DateTime At, string? Door, string? Power)> _lastStored = new();

        public FreezerIoTService(ApplicationDbContext db, IOptions<MqttOptions> mqtt, IFreezerAlarmEngine alarms)
        {
            _db = db;
            _mqtt = mqtt.Value;
            _alarms = alarms;
        }

        private bool ShouldStore(string freezerId, DateTime now, string? door, string? power)
        {
            if (!_lastStored.TryGetValue(freezerId, out var last)) return true;
            if (!string.Equals(last.Door, door) || !string.Equals(last.Power, power)) return true; // تغيّر باب/كهرباء
            return (now - last.At).TotalSeconds >= Math.Max(1, _mqtt.StoreIntervalSeconds);
        }

        public async Task<IngestResult> ProcessIncomingReadingAsync(IngestReadingDto dto)
        {
            var device = await _db.Bio_IoTDevices
                .FirstOrDefaultAsync(d => d.CollectorCode == dto.CollectorCode && d.IsActive);
            if (device == null) return IngestResult.UnknownDevice;

            var freezer = await _db.Bio_Freezers.FirstOrDefaultAsync(f => f.Id == device.FreezerId);
            if (freezer == null) return IngestResult.UnknownDevice;

            var now = DateTime.UtcNow;
            freezer.LastReadingAt = now;                       // آخر مرة الجهاز كان فيها حي

            var result = IngestResult.Heartbeat;
            var store = false;

            if (dto.Temperature.HasValue)
            {
                freezer.CurrentTemperature = dto.Temperature.Value;

                store = ShouldStore(freezer.Id, now, dto.DoorStatus, dto.PowerStatus);
                result = store ? IngestResult.Stored : IngestResult.Live;

                if (store)
                {
                    _db.Bio_FreezerReadings.Add(new Bio_FreezerReading
                    {
                        FreezerId = freezer.Id,
                        Temperature = dto.Temperature.Value,
                        SetTemperature = dto.SetTemperature,
                        AmbientTemperature = dto.AmbientTemperature,
                        HighCompressorRunning = dto.HighCompressorRunning,
                        CryoCompressorRunning = dto.CryoCompressorRunning,
                        DoorStatus = dto.DoorStatus,
                        PowerStatus = dto.PowerStatus,
                        IsOnline = dto.IsOnline,
                        Timestamp = now,
                        RawPayload = dto.RawPayload
                    });
                }
            }

            // ===== محرك الإنذارات =====
            await _alarms.EvaluateReadingAsync(freezer, dto, now);

            await _db.SaveChangesAsync();

            if (store)
                _lastStored[freezer.Id] = (now, dto.DoorStatus, dto.PowerStatus);

            await _alarms.FlushNotificationsAsync();
            return result;
        }

        /// Link a collector to a freezer, or update the existing link (one active device per freezer).
        public async Task<Bio_IoTDeviceDto> LinkDeviceAsync(LinkIoTDeviceDto dto)
        {
            var freezerId = dto.FreezerId.Trim();
            var collector = dto.CollectorCode.Trim();
            var gateway = dto.GatewaySerial.Trim();
            var name = string.IsNullOrWhiteSpace(dto.DeviceName) ? null : dto.DeviceName.Trim();

            if (!await _db.Bio_Freezers.AnyAsync(f => f.Id == freezerId))
                throw new InvalidOperationException($"Freezer '{freezerId}' not found.");

            // The collector may already be registered (active on another freezer, or an old unlinked row)
            var sameCollector = await _db.Bio_IoTDevices.FirstOrDefaultAsync(d => d.CollectorCode == collector);
            if (sameCollector != null && sameCollector.IsActive && sameCollector.FreezerId != freezerId)
                throw new InvalidOperationException($"Collector '{collector}' is already linked to freezer '{sameCollector.FreezerId}'. Unlink it there first.");

            var current = await _db.Bio_IoTDevices.Where(d => d.FreezerId == freezerId && d.IsActive).ToListAsync();

            // Reuse the row that already owns this collector (CollectorCode is unique), otherwise the freezer's current row
            var device = sameCollector ?? current.FirstOrDefault();
            if (device == null)
            {
                device = new Bio_IoTDevice { CreatedAt = DateTime.UtcNow };
                _db.Bio_IoTDevices.Add(device);
            }

            device.FreezerId = freezerId;
            device.CollectorCode = collector;
            device.GatewaySerial = gateway;
            device.DeviceName = name;
            device.IsActive = true;

            // Only one active collector per freezer
            foreach (var other in current.Where(d => d != device))
                other.IsActive = false;

            await _db.SaveChangesAsync();
            return MapDevice(device);
        }

        /// Remove the freezer's IoT link (readings and alarms history stay).
        public async Task<bool> UnlinkDeviceAsync(string freezerId)
        {
            var devices = await _db.Bio_IoTDevices.Where(d => d.FreezerId == freezerId).ToListAsync();
            if (devices.Count == 0) return false;
            _db.Bio_IoTDevices.RemoveRange(devices);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<Bio_FreezerReadingDto>> GetHistoricalAsync(string freezerId, DateTime from, DateTime to)
        {
            return await _db.Bio_FreezerReadings
                .AsNoTracking()
                .Where(r => r.FreezerId == freezerId && r.Timestamp >= from && r.Timestamp <= to)
                .OrderBy(r => r.Timestamp)
                .Select(r => new Bio_FreezerReadingDto
                {
                    Id = r.Id,
                    FreezerId = r.FreezerId,
                    Temperature = r.Temperature,
                    DoorStatus = r.DoorStatus,
                    PowerStatus = r.PowerStatus,
                    IsOnline = r.IsOnline,
                    Timestamp = r.Timestamp
                })
                .ToListAsync();
        }

        public async Task<List<Bio_FreezerAlarmDto>> GetActiveAlarmsAsync(string? freezerId = null)
        {
            // مفتوح = الحالة لسه شغالة، أو اتقفلت ومحدش استلمها
            var query = _db.Bio_FreezerAlarms.AsNoTracking().Where(a => !a.IsCleared || a.AcknowledgedAt == null);
            if (!string.IsNullOrWhiteSpace(freezerId))
                query = query.Where(a => a.FreezerId == freezerId);

            return await query
                .OrderByDescending(a => a.TriggeredAt)
                .Select(a => new Bio_FreezerAlarmDto
                {
                    Id = a.Id,
                    FreezerId = a.FreezerId,
                    AlarmType = a.AlarmType,
                    Level = a.Level,
                    Message = a.Message,
                    TriggeredAt = a.TriggeredAt,
                    AcknowledgedAt = a.AcknowledgedAt,
                    AcknowledgedBy = a.AcknowledgedBy,
                    AckNote = a.AckNote,
                    ClearedAt = a.ClearedAt,
                    EscalatedAt = a.EscalatedAt,
                    PeakValue = a.PeakValue,
                    IsCleared = a.IsCleared
                })
                .ToListAsync();
        }

        /// الاستلام لا يغلق الإنذار: الإغلاق تلقائي لما الحالة ترجع طبيعية.
        public async Task AcknowledgeAlarmAsync(long alarmId, string performedBy, string? note = null)
        {
            var alarm = await _db.Bio_FreezerAlarms.FirstOrDefaultAsync(a => a.Id == alarmId)
                ?? throw new InvalidOperationException("Alarm not found.");

            if (alarm.AcknowledgedAt != null) return;

            alarm.AcknowledgedAt = DateTime.UtcNow;
            alarm.AcknowledgedBy = performedBy;
            var n = note?.Trim();
            alarm.AckNote = string.IsNullOrEmpty(n) ? null : (n.Length <= 500 ? n : n.Substring(0, 500));

            await _db.SaveChangesAsync();
        }

        public async Task<Bio_IoTDeviceDto?> GetDeviceByFreezerAsync(string freezerId)
        {
            var device = await _db.Bio_IoTDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.FreezerId == freezerId && d.IsActive);

            return device == null ? null : MapDevice(device);
        }

        private static Bio_IoTDeviceDto MapDevice(Bio_IoTDevice d) => new()
        {
            Id = d.Id,
            FreezerId = d.FreezerId,
            GatewaySerial = d.GatewaySerial,
            CollectorCode = d.CollectorCode,
            DeviceName = d.DeviceName,
            IsActive = d.IsActive
        };
    }
}