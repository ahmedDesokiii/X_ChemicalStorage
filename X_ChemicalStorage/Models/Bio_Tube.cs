using System.ComponentModel.DataAnnotations.Schema;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.Models
{
    public class Bio_Tube
    {
        [Key]
        [MaxLength(180)]
        public string Id { get; set; } = null!;

        // NEW: physical barcode printed on the tube — scanned in the field.
        // Separate from Id on purpose (per your call): Id is the system slot key,
        // Barcode is whatever's physically printed/stuck on the tube.
        // Recommend a unique index on this column in your DbContext (OnModelCreating):
        //   modelBuilder.Entity<Bio_Tube>().HasIndex(t => t.Barcode).IsUnique();
        [MaxLength(100)]
        public string? Barcode { get; set; }

        [Required]
        [MaxLength(150)]
        public string BoxId { get; set; } = null!;

        [MaxLength(5)]
        public string Position { get; set; } = null!;

        public int Row { get; set; }
        public int Column { get; set; }

        public TubeStatus Status { get; set; } = TubeStatus.Available;

        [MaxLength(100)]
        public string? SampleId { get; set; }

        [MaxLength(200)]
        public string? SampleName { get; set; }

        public DateTime? StoredAt { get; set; }

        [ForeignKey(nameof(BoxId))]
        public Bio_Box Bio_Box { get; set; } = null!;
    }
}