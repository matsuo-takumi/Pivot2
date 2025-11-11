using System;
using System.ComponentModel.DataAnnotations;

namespace Pivot.CodeModule.Models
{
    public class CodeFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty; // e.g., "VEX", "Python"

        public string Tool { get; set; } = string.Empty; // e.g., "Houdini"

        public string Tags { get; set; } = string.Empty; // comma-separated

        public string Content { get; set; } = string.Empty;

        public DateTime Updated { get; set; } = DateTime.Now;
		
        // Soft-delete support: when true the snippet is considered in Trash and can be restored within 30 days.
        public bool IsDeleted { get; set; } = false;

        // Timestamp when item was moved to Trash (UTC). Null when not deleted.
        public DateTime? DeletedAt { get; set; } = null;
    }
}


