namespace X_ChemicalStorage.BioBank.Options
{
    public class BackupOptions
    {
        public const string SectionName = "Backup";

        public bool Enabled { get; set; } = true;

        /// فولدر النسخ على جهاز SQL Server (بيتعمل تلقائياً لو مش موجود).
        public string Directory { get; set; } = @"D:\BioBank_Backups";

        /// نسخة تانية (اختياري): فولدر على قرص/جهاز تاني أو مسار شبكة. فاضي = معطّل.
        public string? SecondaryDirectory { get; set; }

        public string DailyTime { get; set; } = "02:00";                 // بتوقيت TimeZoneId
        public string TimeZoneId { get; set; } = "Egypt Standard Time";

        public int KeepDays { get; set; } = 30;                          // مسح النسخ الأقدم من كده
        public int KeepMinimumFiles { get; set; } = 7;                   // بس ما يقلّش عدد النسخ عن ده أبداً

        /// مرفقات العينات (FASTA وغيره) ملفات على الديسك مش جوه القاعدة: بنعملها ZIP مع كل نسخة.
        public bool IncludeAttachments { get; set; } = true;
        /// فاضي = المسار الافتراضي (App_Data\biobank-uploads جوه فولدر التطبيق)
        public string? AttachmentsDirectory { get; set; }

        public bool UseCompression { get; set; } = false;                // مش مدعوم في كل نسخ SQL (Express)
        public int CommandTimeoutSeconds { get; set; } = 1800;
        public int ManualCooldownSeconds { get; set; } = 30;             // بين نسختين يدويتين
        public int MaxAutoAttemptsPerDay { get; set; } = 3;
        public int RetryAfterMinutes { get; set; } = 30;
        public int StaleWarningHours { get; set; } = 36;                 // تحذير لو آخر نسخة ناجحة أقدم من كده
    }
}