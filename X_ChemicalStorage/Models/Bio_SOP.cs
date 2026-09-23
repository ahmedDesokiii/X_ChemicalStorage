using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.Models
{
    /// <summary>A predefined Standard Operating Procedure samples can be processed under.</summary>
    public class Bio_SOP
    {
        [Key]
        [MaxLength(50)]
        public string Id { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}