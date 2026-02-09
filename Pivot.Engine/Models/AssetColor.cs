using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pivot.Engine.Models
{
    public class AssetColor
    {
        [Key]
        public int Id { get; set; }
        
        // Parent Asset
        public int AssetId { get; set; }
        
        [ForeignKey(nameof(AssetId))]
        public virtual AssetEntity Asset { get; set; } = null!;

        // RGB values
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        
        // Ratio of this color in the image (0.0 - 1.0)
        // Used for sorting by relevance
        public float Ratio { get; set; }
    }
}
