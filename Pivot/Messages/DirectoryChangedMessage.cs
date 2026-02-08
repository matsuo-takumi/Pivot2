using CommunityToolkit.Mvvm.Messaging.Messages;
using Pivot.Engine.Models;

namespace Pivot.Messages
{
    public class DirectoryChangedMessage : ValueChangedMessage<DirectoryChangedMessageData>
    {
        public DirectoryChangedMessage(DirectoryChangedMessageData value) : base(value)
        {
        }
    }

    public class DirectoryChangedMessageData
    {
        public DirectoryCategory Category { get; set; }
        public string Path { get; set; } = string.Empty;
        public ChangeType Type { get; set; }

        public enum ChangeType
        {
            Added,
            Removed
        }
    }

    /// <summary>
    /// Message sent after assets are deleted from a removed directory.
    /// UI should refresh to remove stale data.
    /// </summary>
    public class DirectoryRemovedMessage : ValueChangedMessage<string>
    {
        public DirectoryRemovedMessage(string directoryPath) : base(directoryPath)
        {
        }
    }
}
