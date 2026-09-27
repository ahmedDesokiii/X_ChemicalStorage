using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    public class FreezerIoTService : IFreezerIoTService
    {
        private readonly ApplicationDbContext _db;

        // حدود بسيطة (يمكن نقلها لجدول إعدادات لاحقًا)
        private const decimal HighTempThreshold80 = -70m;
        private const decimal HighTempThreshold150 = -140m;

        public FreezerIoTService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task ProcessIncomingReadingAsync(IngestReadingDto dto)
        {
            var device = await _db.Bio_IoTDevices
                .FirstOrDefaultAsync(d => d.CollectorCode == dto.CollectorCode && d.IsActive);

            if (device == null)
                return; // جهاز غير مسجل — نتجاهل بهدوء

            var freezer = await _db.Bio_Freezers.FirstOrDefaultAsync(f => f.Id == device.FreezerId);
            if (freezer == null) return;

            // حفظ القراءة
            var reading = new Bio_FreezerReading
            {
                FreezerId = freezer.Id,
                Temperature = dto.Temperature,
                DoorStatus = dto.DoorStatus,
                PowerStatus = dto.PowerStatus,
                IsOnline = dto.IsOnline,
                Timestamp = DateTime.UtcNow,
                RawPayload = dto.RawPayload
            };
            _db.Bio_FreezerReadings.Add(reading);

            // تحديث الحالة الحية على الفريزر
            freezer.CurrentTemperature = dto.Temperature;
            freezer.LastReadingAt = reading.Timestamp;

            if (!dto.IsOnline)
            {
                freezer.Status = FreezerStatus.Offline;
                await EnsureAlarmAsync(freezer.Id, "Offline", 1, "Gateway is offline");
            }
            else
            {
                // فحص تجاوز الحرارة
                decimal threshold = freezer.Type == FreezerType.Minus150
                    ? HighTempThreshold150
                    : HighTempThreshold80;

                if (dto.Temperature > threshold)
                {
                    freezer.Status = FreezerStatus.Alarm;
                    await EnsureAlarmAsync(freezer.Id, "HighTemp", 1,
                        $"Temperature {dto.Temperature:F1}°C exceeded threshold {threshold}°C");
                }
                else if (freezer.Status == FreezerStatus.Alarm || freezer.Status == FreezerStatus.Offline)
                {
                    // لو مفيش إنذار نشط، نرجّع Online
                    var hasActive = await _db.Bio_FreezerAlarms
                        .AnyAsync(a => a.FreezerId == freezer.Id && !a.IsCleared);
                    if (!hasActive)
                        freezer.Status = FreezerStatus.Online;
                }
                else
                {
                    freezer.Status = FreezerStatus.Online;
                }
            }

            await _db.SaveChangesAsync();
        }

        private async Task EnsureAlarmAsync(string freezerId, string type, int level, string message)
        {
            var exists = await _db.Bio_FreezerAlarms
                .AnyAsync(a => a.FreezerId == freezerId && a.AlarmType == type && !a.IsCleared);

            if (exists) return;

            _db.Bio_FreezerAlarms.Add(new Bio_FreezerAlarm
            {
                FreezerId = freezerId,
                AlarmType = type,
                Level = level,
                Message = message,
                TriggeredAt = DateTime.UtcNow
            });
        }

        public async Task<Bio_IoTDeviceDto> LinkDeviceAsync(LinkIoTDeviceDto dto)
        {
            var freezer = await _db.Bio_Freezers.FirstOrDefaultAsync(f => f.Id == dto.FreezerId)
                ?? throw new InvalidOperationException($"Freezer '{dto.FreezerId}' not found.");

            // منع تكرار CollectorCode
            if (await _db.Bio_IoTDevices.AnyAsync(d => d.CollectorCode == dto.CollectorCode))
                throw new InvalidOperationException($"CollectorCode '{dto.CollectorCode}' is already linked.");

            var device = new Bio_IoTDevice
            {
                FreezerId = dto.FreezerId,
                GatewaySerial = dto.GatewaySerial.Trim(),
                CollectorCode = dto.CollectorCode.Trim(),
                DeviceName = dto.DeviceName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Bio_IoTDevices.Add(device);
            await _db.SaveChangesAsync();

            return MapDevice(device);
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
            var query = _db.Bio_FreezerAlarms.AsNoTracking().Where(a => !a.IsCleared);
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
                    IsCleared = a.IsCleared
                })
                .ToListAsync();
        }

        public async Task AcknowledgeAlarmAsync(long alarmId, string performedBy)
        {
            var alarm = await _db.Bio_FreezerAlarms.FirstOrDefaultAsync(a => a.Id == alarmId)
                ?? throw new InvalidOperationException("Alarm not found.");

            if (alarm.IsCleared) return;

            alarm.IsCleared = true;
            alarm.AcknowledgedAt = DateTime.UtcNow;
            alarm.AcknowledgedBy = performedBy;

            // لو مفيش إنذارات تانية نشطة → نرجّع الحالة Online
            var stillActive = await _db.Bio_FreezerAlarms
                .AnyAsync(a => a.FreezerId == alarm.FreezerId && !a.IsCleared && a.Id != alarmId);

            if (!stillActive)
            {
                var freezer = await _db.Bio_Freezers.FirstOrDefaultAsync(f => f.Id == alarm.FreezerId);
                if (freezer != null && freezer.Status == FreezerStatus.Alarm)
                    freezer.Status = FreezerStatus.Online;
            }

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