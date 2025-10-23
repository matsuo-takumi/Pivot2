using CommunityToolkit.Mvvm.Messaging.Messages;
using Pivot.Models;

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
}
