using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    /// إشعارات النظام (مش مرتبطة بفريزر): بتتكتب في نفس طابور الإشعارات وبتتبعت لجهات المستوى 1.
    public class SystemNoticeService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEnumerable<INotificationChannel> _channels;

        public SystemNoticeService(ApplicationDbContext db, IEnumerable<INotificationChannel> channels)
        {
            _db = db;
            _channels = channels;
        }

        public async Task<int> NotifyAsync(string subject, string body, DateTime nowUtc, CancellationToken ct = default)
        {
            var contacts = await _db.AlarmContacts.Where(c => c.IsActive && c.Tier == 1 && c.FreezerId == null).ToListAsync(ct);
            int created = 0;
            foreach (var c in contacts)
                foreach (var (channel, address) in OutboxAlarmNotifier.Targets(c))
                {
                    var ch = _channels.FirstOrDefault(x => x.Name == channel);
                    if (ch == null || !ch.IsEnabled) continue;
                    _db.AlarmNotifications.Add(new Bio_AlarmNotification
                    {
                        AlarmId = 0,
                        FreezerId = "-",
                        ContactId = c.Id,
                        ContactName = c.Name,
                        Channel = channel,
                        Kind = "System",
                        Recipient = address,
                        Subject = Str.Trunc(subject, 200),
                        Message = Str.Trunc(body, 300),
                        Status = Bio_NotificationStatus.Pending,
                        CreatedAt = nowUtc,
                        NextAttemptAt = nowUtc
                    });
                    created++;
                }
            if (created > 0) await _db.SaveChangesAsync(ct);
            return created;
        }
    }
}