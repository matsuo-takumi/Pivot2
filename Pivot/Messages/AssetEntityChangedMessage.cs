using Pivot.Engine.Models;

namespace Pivot.Messages
{
    public class AssetEntityChangedMessage
    {
        public AssetEntity Asset { get; }
        public ChangeType Type { get; } // Changed from ChangeType to Type to match usage in CodeListViewModel (message.Type)

        public AssetEntityChangedMessage(AssetEntity asset, ChangeType changeType = ChangeType.Updated)
        {
            Asset = asset;
            Type = changeType;
        }

        public enum ChangeType
        {
            Added,
            Updated,
            Deleted // User code said Removed, but existing code uses Deleted. Using Deleted to match usage.
        }
    }
}
