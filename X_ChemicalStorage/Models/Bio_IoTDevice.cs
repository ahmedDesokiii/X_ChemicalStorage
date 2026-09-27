using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X_ChemicalStorage.Models
{
    public class Bio_IoTDevice
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string FreezerId { get; set; } = null!;

        [Required, MaxLength(100)]
        public string GatewaySerial { get; set; } = null!;

        [Required, MaxLength(100)]
        public string CollectorCode { get; set; } = null!;

        [MaxLength(150)]
        public string? DeviceName { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(FreezerId))]
        public Bio_Freezer Freezer { get; set; } = null!;
    }
}