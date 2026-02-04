using System.Collections.Generic;

namespace Pivot.CodeModule.Messages
{
    /// <summary>
    /// Message sent when tags are updated globally (e.g., after snippet creation).
    /// </summary>
    public record TagsUpdatedMessage(IEnumerable<string> Tags);
}
