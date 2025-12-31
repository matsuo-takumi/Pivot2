using Pivot.Models;

namespace Pivot.Messages
{
    /// <summary>
    /// アセットの同期完了を通知するメッセージ
    /// </summary>
    public class AssetsSyncedMessage
    {
        public int AddedCount { get; }
        public int UpdatedCount { get; }
        public int DeletedCount { get; }
        public bool IsFullSync { get; }

        public AssetsSyncedMessage(int added, int updated, int deleted, bool isFullSync = false)
        {
            AddedCount = added;
            UpdatedCount = updated;
            DeletedCount = deleted;
            IsFullSync = isFullSync;
        }
    }

    /// <summary>
    /// 個別アセットの変更を通知するメッセージ (AssetEntity統一版)
    /// </summary>
    public class AssetEntityChangedMessage
    {
        public enum ChangeType { Added, Updated, Deleted }
        
        public AssetEntity? Asset { get; }
        public string FilePath { get; }
        public ChangeType Type { get; }

        public AssetEntityChangedMessage(AssetEntity? asset, string filePath, ChangeType type)
        {
            Asset = asset;
            FilePath = filePath;
            Type = type;
        }
    }

    /// <summary>
    /// 一括アセット変更を通知するメッセージ
    /// </summary>
    public class BulkAssetsChangedMessage
    {
        public System.Collections.Generic.List<ItemChangeData<AssetEntity>> Changes { get; }

        public BulkAssetsChangedMessage(System.Collections.Generic.List<ItemChangeData<AssetEntity>> changes)
        {
            Changes = changes;
        }
    }
}
