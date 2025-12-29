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
    /// 個別アセットファイルの変更を通知するメッセージ
    /// </summary>
    public class AssetFileChangedMessage
    {
        public enum ChangeType { Added, Updated, Deleted }
        
        public AssetFile? Asset { get; }
        public string FilePath { get; }
        public ChangeType Type { get; }

        public AssetFileChangedMessage(AssetFile? asset, string filePath, ChangeType type)
        {
            Asset = asset;
            FilePath = filePath;
            Type = type;
        }
    }
}

