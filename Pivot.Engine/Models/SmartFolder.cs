using System.ComponentModel.DataAnnotations;

namespace Pivot.Engine.Models
{
    public class SmartFolder
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string IconGlyph { get; set; } = "\uE8B7"; // Default icon
        
        public int SortOrder { get; set; }

        // JSON serialized FilterCriteria
        [MaxLength(4096)]
        public string CriteriaJson { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
