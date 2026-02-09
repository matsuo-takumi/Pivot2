using Pivot.Engine.Models;

namespace Pivot.Messages
{
    public class AssetEntityChangedMessage
    {
        public AssetEntity? Asset { get; }
        public AssetChangeType Type { get; }

        public AssetEntityChangedMessage(AssetEntity? asset, AssetChangeType type = AssetChangeType.Updated)
        {
            Asset = asset;
            Type = type;
        }
    }

    public enum AssetChangeType
    {
        Added,
        Updated,
        Removed,
        Deleted
    }
}
