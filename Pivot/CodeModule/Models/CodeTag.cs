using System.ComponentModel.DataAnnotations;

namespace Pivot.CodeModule.Models
{
    public class CodeTag
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}


