using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    /// بيحوّل حدث الإنذار إلى صفوف Pending في جدول الإشعارات (سريع). الإرسال الفعلي بيتم في الخلفية.
    /// التوجيه: Raised => Tier 1 | Escalated => Tier 2 (أو Tier 1 لو مفيش) | Cleared => اللي اتبلّغوا قبل كده.
    public class OutboxAlarmNotifier : IAlarmNotifier
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IEnumerable<INotificationChannel> _channels;
        private readonly NotificationOptions _opt;
        private readonly ILogger<OutboxAlarmNotifier> _log;

        public OutboxAlarmNotifier(IServiceScopeFactory scopes, IEnumerable<INotificationChannel> channels,
            IOptions<NotificationOptions> opt, ILogger<OutboxAlarmNotifier> log)
        {
            _scopes = scopes;
            _channels = channels;
            _opt = opt.Value;
            _log = log;
        }

        public async Task NotifyAsync(AlarmNotification n, CancellationToken ct = default)
        {
            _log.LogWarning("ALARM {Kind} #{Id} freezer={Freezer} type={Type} level={Level}: {Msg}",
                n.Kind, n.AlarmId, n.FreezerId, n.AlarmType, n.Level, n.Message);

            if (n.Kind == AlarmEventKind.Cleared && !_opt.NotifyOnCleared) return;

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;

            var contacts = await db.AlarmContacts
                .Where(c => c.IsActive && (c.FreezerId == null || c.FreezerId == n.FreezerId))
                .ToListAsync(ct);

            List<Bio_AlarmContact> recipients;
            switch (n.Kind)
            {
                case AlarmEventKind.Escalated:
                    var t2 = contacts.Where(c => c.Tier == 2).ToList();
                    recipients = t2.Count > 0 ? t2 : contacts.Where(c => c.Tier == 1).ToList();
                    break;
                case AlarmEventKind.Reminder:
                    // التذكير للمسؤولين حالياً: مستوى 1 دايماً، ولو الإنذار اتصعّد يتضاف مستوى 2
                    recipients = contacts.Where(c => c.Tier == 1 || (n.Level >= 2 && c.Tier == 2)).ToList();
                    break;
                case AlarmEventKind.Cleared:
                    var notified = await db.AlarmNotifications
                        .Where(x => x.AlarmId == n.AlarmId && x.ContactId != null
                                    && (x.Kind == "Raised" || x.Kind == "Escalated" || x.Kind == "Reminder")
                                    && x.Status != Bio_NotificationStatus.Skipped)
                        .Select(x => x.ContactId!.Value).ToListAsync(ct);
                    recipients = contacts.Where(c => notified.Contains(c.Id)).ToList();
                    break;
                default:
                    recipients = contacts.Where(c => c.Tier == 1).ToList();
                    break;
            }

            var local = ToLocal(now);
            var (subject, body) = AlarmMessageBuilder.Build(n, local, ToLocal(n.TriggeredAt));
            var kind = n.Kind.ToString();
            int created = 0, duplicates = 0;

            foreach (var c in recipients)
                foreach (var (channelName, address) in Targets(c))
                {
                    var ch = _channels.FirstOrDefault(x => x.Name == channelName);
                    if (ch == null || !ch.IsEnabled) continue;

                    // التذكيرات بتتكرر بطبيعتها، فمفيش فحص تكرار ليها
                    if (n.Kind != AlarmEventKind.Reminder)
                    {
                        var exists = await db.AlarmNotifications.AnyAsync(x =>
                            x.AlarmId == n.AlarmId && x.ContactId == c.Id && x.Channel == channelName && x.Kind == kind, ct);
                        if (exists) { duplicates++; continue; }
                    }

                    db.AlarmNotifications.Add(new Bio_AlarmNotification
                    {
                        AlarmId = n.AlarmId,
                        FreezerId = n.FreezerId,
                        ContactId = c.Id,
                        ContactName = c.Name,
                        Channel = channelName,
                        Kind = kind,
                        Recipient = address,
                        Subject = subject,
                        Message = body,
                        Status = Bio_NotificationStatus.Pending,
                        CreatedAt = now,
                        NextAttemptAt = now
                    });
                    created++;
                }

            if (created == 0 && duplicates == 0 && n.Kind != AlarmEventKind.Cleared && n.Kind != AlarmEventKind.Reminder)
            {
                // عشان يبان في السجل ليه محدش اتبلّغ
                var reason = recipients.Count == 0
                    ? "No active contacts configured for this freezer/tier"
                    : "Contacts found but no enabled channel matches their details";
                _log.LogError("Alarm #{Id} on {Freezer}: nobody was notified. {Reason}", n.AlarmId, n.FreezerId, reason);
                db.AlarmNotifications.Add(new Bio_AlarmNotification
                {
                    AlarmId = n.AlarmId,
                    FreezerId = n.FreezerId,
                    ContactName = "(nobody)",
                    Channel = "-",
                    Kind = kind,
                    Subject = subject,
                    Message = body,
                    Status = Bio_NotificationStatus.Skipped,
                    Error = reason,
                    CreatedAt = now,
                    NextAttemptAt = now
                });
            }

            await db.SaveChangesAsync(ct);
        }

        public static IEnumerable<(string Channel, string Address)> Targets(Bio_AlarmContact c)
        {
            if (!string.IsNullOrWhiteSpace(c.Phone)) yield return (Bio_NotificationChannels.Sms, c.Phone!);
            if (!string.IsNullOrWhiteSpace(c.Email)) yield return (Bio_NotificationChannels.Email, c.Email!);
            if (!string.IsNullOrWhiteSpace(c.TelegramChatId)) yield return (Bio_NotificationChannels.Telegram, c.TelegramChatId!);
        }

        private DateTime ToLocal(DateTime utc)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(_opt.TimeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            }
            catch
            {
                try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")); }
                catch { return utc.AddHours(2); }
            }
        }
    }
}