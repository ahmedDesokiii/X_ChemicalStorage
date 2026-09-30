using System.ComponentModel.DataAnnotations;

namespace X_ChemicalStorage.Models
{
    public static class Bio_NotificationStatus
    {
        public const string Pending = "Pending";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
        public const string Skipped = "Skipped";
    }

    public static class Bio_NotificationChannels
    {
        public const string Sms = "SMS";
        public const string Email = "Email";
        public const string Telegram = "Telegram";
    }

    /// سجل الإشعارات (وفي نفس الوقت طابور الإرسال: Pending => Sent/Failed).
    public class Bio_AlarmNotification
    {
        [Key]
        public long Id { get; set; }

        public long AlarmId { get; set; }                 // 0 = رسالة اختبار

        [Required, MaxLength(50)]
        public string FreezerId { get; set; } = "";

        public int? ContactId { get; set; }

        [MaxLength(100)]
        public string ContactName { get; set; } = "";

        [Required, MaxLength(20)]
        public string Channel { get; set; } = "";

        [Required, MaxLength(20)]
        public string Kind { get; set; } = "";            // Raised | Escalated | Cleared | Test

        [MaxLength(200)]
        public string Recipient { get; set; } = "";

        [MaxLength(200)]
        public string Subject { get; set; } = "";

        [MaxLength(1000)]
        public string Message { get; set; } = "";

        [Required, MaxLength(20)]
        public string Status { get; set; } = Bio_NotificationStatus.Pending;

        public int Attempts { get; set; }

        [MaxLength(500)]
        public string? Error { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
    }
}