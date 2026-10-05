using System.Globalization;

namespace X_ChemicalStorage.BioBank.Services
{
    /// منطق "امتى ناخد النسخة" (دوال نقية بدون قاعدة بيانات).
    public static class BackupScheduler
    {
        public static TimeSpan ParseDailyTime(string? s)
            => TimeSpan.TryParseExact(s?.Trim(), new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out var t)
               ? t : TimeSpan.FromHours(2);

        /// آخر موعد مجدول فات (النهارده لو عدّى وقته، وإلا إمبارح).
        public static DateTime LastSlot(DateTime localNow, TimeSpan dailyTime)
        {
            var today = localNow.Date + dailyTime;
            return localNow >= today ? today : today.AddDays(-1);
        }

        public static DateTime NextSlot(DateTime localNow, TimeSpan dailyTime)
        {
            var today = localNow.Date + dailyTime;
            return localNow < today ? today : today.AddDays(1);
        }

        /// مستحقة لو مفيش نسخة تلقائية ناجحة بعد آخر موعد مجدول، ولسه ماعدّيناش حد المحاولات،
        /// وآخر محاولة فاشلة عدّى عليها وقت كفاية. (ده بيغطي كمان لو التطبيق كان واقف وقت الموعد.)
        public static bool IsDue(DateTime localNow, TimeSpan dailyTime,
            IReadOnlyList<(DateTime StartedLocal, bool Success)> autoAttempts, int maxAttempts, int retryAfterMinutes)
        {
            var slot = LastSlot(localNow, dailyTime);
            var since = autoAttempts.Where(a => a.StartedLocal >= slot).OrderBy(a => a.StartedLocal).ToList();

            if (since.Any(a => a.Success)) return false;
            if (since.Count >= Math.Max(1, maxAttempts)) return false;
            if (since.Count > 0 && (localNow - since.Last().StartedLocal).TotalMinutes < Math.Max(1, retryAfterMinutes)) return false;
            return true;
        }

        /// هل السيرفر على نفس الجهاز (يعني الفولدر قدامنا)؟
        public static bool IsLocalServer(string? dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource)) return false;
            var s = dataSource.Trim();
            if (s.StartsWith("np:", StringComparison.OrdinalIgnoreCase)) s = s.Substring(3);
            if (s.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) s = s.Substring(4);
            if (s.StartsWith(@"\\.\pipe", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase)) return true;

            var host = s.Split('\\')[0].Split(',')[0].Trim();
            if (host is "." or "(local)" or "localhost" or "127.0.0.1" or "::1") return true;
            var me = Environment.MachineName;
            return host.Equals(me, StringComparison.OrdinalIgnoreCase)
                || host.StartsWith(me + ".", StringComparison.OrdinalIgnoreCase);
        }
    }
}