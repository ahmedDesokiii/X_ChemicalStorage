using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Bio_FreezerAlarm
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(50)]
        public string FreezerId { get; set; } = null!;

        [Required, MaxLength(50)]
        public string AlarmType { get; set; } = null!;   // انظر Bio_AlarmTypes

        public int Level { get; set; } = 1;               // 1 or 2

        [Required, MaxLength(500)]
        public string Message { get; set; } = null!;

        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcknowledgedAt { get; set; }

        [MaxLength(100)]
        public string? AcknowledgedBy { get; set; }

        public bool IsCleared { get; set; }

        // ===== جديد =====
        [MaxLength(500)]
        public string? AckNote { get; set; }        // سبب المشكلة (يكتبه المستخدم عند الاستلام)

        public DateTime? ClearedAt { get; set; }    // وقت عودة الحالة للطبيعي (تلقائي)
        public DateTime? EscalatedAt { get; set; }  // وقت التصعيد للمستوى 2
        public decimal? PeakValue { get; set; }     // أسوأ قراءة أثناء الإنذار

        // ===== التذكير المتكرر =====
        public DateTime? LastNotifiedAt { get; set; }   // آخر مرة اتبعت فيها إشعار (إنذار/تصعيد/تذكير)
        public int ReminderCount { get; set; }          // عدد التذكيرات اللي اتبعتت

        [ForeignKey(nameof(FreezerId))]
        public Bio_Freezer Freezer { get; set; } = null!;
    }
}