using System.ComponentModel.DataAnnotations;

namespace X_ChemicalStorage.Models
{
    /// جهة اتصال لاستلام إنذارات الفريزرات.
    public class Bio_AlarmContact
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = "Ahmed Desoky";

        [MaxLength(30)]
        public string? Phone { get; set; } = "+2001000025879";          // بصيغة دولية +2010xxxxxxxx (SMS)

        [MaxLength(200)]
        public string? Email { get; set; } = "adesokiii88@gmail.com";

        [MaxLength(50)]
        public string? TelegramChatId { get; set; }

        /// 1 = المستوى الأول (بيستلم الإنذار فوراً) | 2 = المستوى الثاني (بيستلم التصعيد)
        public int Tier { get; set; } = 1;

        /// null = كل الفريزرات
        [MaxLength(50)]
        public string? FreezerId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}