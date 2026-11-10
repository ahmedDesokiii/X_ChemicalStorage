using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    /// <summary>
    /// Moves a lab's Bio Bank setup (freezers, collector links, alarm limits, alarm contacts)
    /// from one database to another as a small JSON file. Samples, readings and alarm history
    /// are never included. Import only adds what's missing, so running it twice is safe.
    /// </summary>
    public interface ISiteSetupService
    {
        Task<SiteSetupFile> ExportAsync();
        Task<SiteSetupResult> ImportAsync(SiteSetupFile file, string importedBy);
    }

    public class SiteSetupFile
    {
        public int Version { get; set; } = 1;
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
        public string? ExportedFrom { get; set; }
        public List<SiteSetupFreezer> Freezers { get; set; } = new();
        public List<SiteSetupContact> Contacts { get; set; } = new();
    }

    public class SiteSetupFreezer
    {
        public string Id { get; set; } = "";
        public FreezerType Type { get; set; }
        public string? Barcode { get; set; }
        public int ShelvesCount { get; set; }
        public int RacksPerShelf { get; set; }
        public int DrawersPerRack { get; set; }
        public int BoxesPerDrawer { get; set; }
        public int TubesPerBox { get; set; }

        public string? CollectorCode { get; set; }
        public string? GatewaySerial { get; set; }
        public string? DeviceName { get; set; }

        public decimal? HighTempLimit { get; set; }
        public decimal? LowTempLimit { get; set; }
        public int? TempAlarmDelayMinutes { get; set; }
        public int? DoorOpenMinutes { get; set; }
        public int? OfflineAfterMinutes { get; set; }
        public int? EscalationMinutes { get; set; }
    }

    public class SiteSetupContact
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TelegramChatId { get; set; }
        public int Tier { get; set; } = 1;
        public string? FreezerId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SiteSetupResult
    {
        public int FreezersCreated { get; set; }
        public int FreezersSkipped { get; set; }
        public int LinksSet { get; set; }
        public int ContactsAdded { get; set; }
        public List<string> Problems { get; set; } = new();
        public string Summary =>
            $"{FreezersCreated} freezer(s) created, {FreezersSkipped} already existed, {LinksSet} collector link(s), {ContactsAdded} contact(s) added"
            + (Problems.Count > 0 ? $", {Problems.Count} problem(s)" : "");
    }

    public class SiteSetupService : ISiteSetupService
    {
        private readonly ApplicationDbContext _db;
        private readonly IBioBankService _bioBank;
        private readonly IFreezerIoTService _iot;
        private readonly ILogger<SiteSetupService> _log;

        public SiteSetupService(ApplicationDbContext db, IBioBankService bioBank, IFreezerIoTService iot, ILogger<SiteSetupService> log)
        {
            _db = db; _bioBank = bioBank; _iot = iot; _log = log;
        }

        public async Task<SiteSetupFile> ExportAsync()
        {
            var freezers = await _db.Bio_Freezers.AsNoTracking().OrderBy(f => f.Id).ToListAsync();
            var settings = await _db.Bio_FreezerSettings.AsNoTracking().ToDictionaryAsync(s => s.FreezerId);
            var devices = await _db.Bio_IoTDevices.AsNoTracking().Where(d => d.IsActive).ToListAsync();
            var contacts = await _db.AlarmContacts.AsNoTracking().OrderBy(c => c.Tier).ThenBy(c => c.Name).ToListAsync();

            var file = new SiteSetupFile { ExportedFrom = Environment.MachineName };
            foreach (var f in freezers)
            {
                settings.TryGetValue(f.Id, out var s);
                var d = devices.FirstOrDefault(x => x.FreezerId == f.Id);
                file.Freezers.Add(new SiteSetupFreezer
                {
                    Id = f.Id, Type = f.Type, Barcode = f.Barcode,
                    ShelvesCount = f.ShelvesCount, RacksPerShelf = f.RacksPerShelf, DrawersPerRack = f.DrawersPerRack,
                    BoxesPerDrawer = f.BoxesPerDrawer, TubesPerBox = f.TubesPerBox,
                    CollectorCode = d?.CollectorCode, GatewaySerial = d?.GatewaySerial, DeviceName = d?.DeviceName,
                    HighTempLimit = s?.HighTempLimit, LowTempLimit = s?.LowTempLimit,
                    TempAlarmDelayMinutes = s?.TempAlarmDelayMinutes, DoorOpenMinutes = s?.DoorOpenMinutes,
                    OfflineAfterMinutes = s?.OfflineAfterMinutes, EscalationMinutes = s?.EscalationMinutes
                });
            }
            file.Contacts = contacts.Select(c => new SiteSetupContact
            {
                Name = c.Name, Phone = c.Phone, Email = c.Email, TelegramChatId = c.TelegramChatId,
                Tier = c.Tier, FreezerId = c.FreezerId, IsActive = c.IsActive
            }).ToList();
            return file;
        }

        public async Task<SiteSetupResult> ImportAsync(SiteSetupFile file, string importedBy)
        {
            var r = new SiteSetupResult();
            if (file?.Freezers == null) { r.Problems.Add("The file has no freezers."); return r; }

            foreach (var f in file.Freezers.Where(x => !string.IsNullOrWhiteSpace(x.Id)))
            {
                var id = f.Id.Trim().ToUpperInvariant();
                try
                {
                    if (await _db.Bio_Freezers.AnyAsync(x => x.Id == id)) r.FreezersSkipped++;
                    else
                    {
                        // same service the Bio Bank page uses, so shelves/racks/boxes/tubes are generated
                        await _bioBank.CreateFreezerAsync(new Bio_CreateFreezerDto
                        {
                            Id = id, Type = f.Type, Barcode = f.Barcode,
                            ShelvesCount = f.ShelvesCount, RacksPerShelf = f.RacksPerShelf, DrawersPerRack = f.DrawersPerRack,
                            BoxesPerDrawer = f.BoxesPerDrawer, TubesPerBox = f.TubesPerBox > 0 ? f.TubesPerBox : 100
                        });
                        r.FreezersCreated++;
                    }

                    // alarm limits: take the file's values; commissioning on, so no alarm until the first good reading
                    var s = await _db.Bio_FreezerSettings.FirstOrDefaultAsync(x => x.FreezerId == id);
                    if (s == null)
                    {
                        s = Bio_FreezerSettings.CreateDefault(id, f.Type);
                        _db.Bio_FreezerSettings.Add(s);
                    }
                    if (f.HighTempLimit.HasValue && f.LowTempLimit.HasValue && f.HighTempLimit > f.LowTempLimit)
                    {
                        s.HighTempLimit = f.HighTempLimit.Value;
                        s.LowTempLimit = f.LowTempLimit.Value;
                    }
                    if (f.TempAlarmDelayMinutes is > 0) s.TempAlarmDelayMinutes = f.TempAlarmDelayMinutes.Value;
                    if (f.DoorOpenMinutes is > 0) s.DoorOpenMinutes = f.DoorOpenMinutes.Value;
                    if (f.OfflineAfterMinutes is > 0) s.OfflineAfterMinutes = f.OfflineAfterMinutes.Value;
                    if (f.EscalationMinutes is > 0) s.EscalationMinutes = f.EscalationMinutes.Value;
                    s.IsCommissioning = true;
                    s.CommissioningEndedAt = null;
                    s.UpdatedAt = DateTime.UtcNow;
                    s.UpdatedBy = importedBy;
                    await _db.SaveChangesAsync();

                    if (!string.IsNullOrWhiteSpace(f.CollectorCode))
                    {
                        await _iot.LinkDeviceAsync(new LinkIoTDeviceDto
                        {
                            FreezerId = id,
                            CollectorCode = f.CollectorCode.Trim(),
                            GatewaySerial = string.IsNullOrWhiteSpace(f.GatewaySerial) ? f.CollectorCode.Trim() : f.GatewaySerial.Trim(),
                            DeviceName = f.DeviceName
                        });
                        r.LinksSet++;
                    }
                }
                catch (Exception ex)
                {
                    _db.ChangeTracker.Clear();
                    r.Problems.Add($"{id}: {ex.Message}");
                    _log.LogWarning(ex, "Site setup: freezer {Id} not fully imported", id);
                }
            }

            var existing = await _db.AlarmContacts.AsNoTracking().ToListAsync();
            foreach (var c in (file.Contacts ?? new()).Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                bool dup = existing.Any(e => string.Equals(e.Name, c.Name, StringComparison.OrdinalIgnoreCase)
                                          && (e.TelegramChatId ?? "") == (c.TelegramChatId ?? "")
                                          && (e.Phone ?? "") == (c.Phone ?? ""));
                if (dup) continue;
                var freezerId = string.IsNullOrWhiteSpace(c.FreezerId) ? null : c.FreezerId.Trim().ToUpperInvariant();
                if (freezerId != null && !await _db.Bio_Freezers.AnyAsync(x => x.Id == freezerId)) freezerId = null;
                _db.AlarmContacts.Add(new Bio_AlarmContact
                {
                    Name = c.Name.Trim(), Phone = c.Phone, Email = c.Email, TelegramChatId = c.TelegramChatId,
                    Tier = c.Tier is 1 or 2 ? c.Tier : 1, FreezerId = freezerId, IsActive = c.IsActive,
                    CreatedAt = DateTime.UtcNow
                });
                r.ContactsAdded++;
            }
            await _db.SaveChangesAsync();

            _log.LogInformation("Site setup imported by {By}: {Summary}", importedBy, r.Summary);
            foreach (var p in r.Problems) _log.LogWarning("Site setup problem: {Problem}", p);
            return r;
        }
    }
}
