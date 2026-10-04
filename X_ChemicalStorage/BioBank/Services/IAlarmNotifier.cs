using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.BioBank.Services
{
    public enum AlarmEventKind { Raised, Escalated, Cleared, Reminder }

    public record AlarmNotification(
        AlarmEventKind Kind,
        long AlarmId,
        string FreezerId,
        string AlarmType,
        int Level,
        string Message,
        DateTime TriggeredAt,
        int Sequence = 0);        // رقم التذكير (للتذكيرات فقط)

    /// نقطة توصيل الإشعارات (SMS / صوت محلي / ...). الفشل هنا ما بيوقفش استقبال القراءات.
    public interface IAlarmNotifier
    {
        Task NotifyAsync(AlarmNotification n, CancellationToken ct = default);
    }

    /// مؤقتاً: بيكتب في اللوج بس. البند 4 هيستبدله بالإرسال الفعلي.
    public class LoggingAlarmNotifier : IAlarmNotifier
    {
        private readonly ILogger<LoggingAlarmNotifier> _log;
        public LoggingAlarmNotifier(ILogger<LoggingAlarmNotifier> log) => _log = log;

        public Task NotifyAsync(AlarmNotification n, CancellationToken ct = default)
        {
            var level = n.Kind == AlarmEventKind.Cleared ? LogLevel.Information : LogLevel.Warning;
            _log.Log(level, "ALARM {Kind} #{Id} freezer={Freezer} type={Type} level={Level}: {Msg}",
                n.Kind, n.AlarmId, n.FreezerId, n.AlarmType, n.Level, n.Message);
            return Task.CompletedTask;
        }
    }
}