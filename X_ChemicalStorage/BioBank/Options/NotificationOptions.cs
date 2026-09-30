namespace X_ChemicalStorage.BioBank.Options
{
    public class NotificationOptions
    {
        public const string SectionName = "Notifications";

        public bool NotifyOnCleared { get; set; } = true;
        public int MaxAttempts { get; set; } = 5;
        public int WorkerIntervalSeconds { get; set; } = 5;
        public int ExpireAfterHours { get; set; } = 12;     // إشعار متأخر أكتر من كده يتلغي
        public string TimeZoneId { get; set; } = "Egypt Standard Time";

        public SmsHttpOptions Sms { get; set; } = new();
        public EmailOptions Email { get; set; } = new();
        public TelegramOptions Telegram { get; set; } = new();
    }

    /// أي مزوّد SMS بيقبل HTTP. المتغيرات المتاحة في القوالب: {to} و {message}
    public class SmsHttpOptions
    {
        public bool Enabled { get; set; } = false;
        public string Url { get; set; } = "";
        public string Method { get; set; } = "POST";
        public string BodyFormat { get; set; } = "json";       // json | form
        public string BodyTemplate { get; set; } = "{\"to\":\"{to}\",\"message\":\"{message}\"}";
        public Dictionary<string, string> Headers { get; set; } = new();
        public string SuccessContains { get; set; } = "";       // اختياري: نص لازم يظهر في رد المزوّد
        public int TimeoutSeconds { get; set; } = 15;
    }

    public class EmailOptions
    {
        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "";
        public string FromName { get; set; } = "BioBank Alerts";
    }

    public class TelegramOptions
    {
        public bool Enabled { get; set; } = false;
        public string BotToken { get; set; } = "";
        public string ApiBaseUrl { get; set; } = "https://api.telegram.org";
    }
}