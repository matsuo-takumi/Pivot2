using Pivot.Engine.Models;

namespace Pivot.Messages
{
    public class AssetEntityChangedMessage
    {
        public AssetEntity Asset { get; }
        public ChangeType ChangeType { get; }

        public AssetEntityChangedMessage(AssetEntity asset, ChangeType changeType = ChangeType.Updated)
        {
            Asset = asset;
            ChangeType = changeType;
        }
    }

    public enum ChangeType
    {
        Added,
        Updated,
        Removed
    }
}
