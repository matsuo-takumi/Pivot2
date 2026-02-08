using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;
using Pivot.Engine.Models; // BackdropType moved to Models

namespace Pivot.Messages
{
    public class BackdropTypeChangedMessage : ValueChangedMessage<BackdropType>
    {
        public BackdropTypeChangedMessage(BackdropType value) : base(value)
        {
        }
    }

    public class ScanCompletedMessage : ValueChangedMessage<bool>
    {
        public ScanCompletedMessage(bool value) : base(value)
        {
        }
    }

    /// <summary>
    /// Asset ファイルが追加、更新、削除されたときに送信されるメッセージ
    /// </summary>
    public class AssetChangedMessage : ValueChangedMessage<AssetChangedMessageData>
    {
        public AssetChangedMessage(AssetChangedMessageData value) : base(value)
        {
        }
    }

    public class AssetChangedMessageData
    {
        public enum ChangeType { Added, Updated, Deleted }

        public ChangeType Type { get; set; }
        public string FilePath { get; set; } = string.Empty;

        public AssetChangedMessageData(ChangeType type, string filePath)
        {
            Type = type;
            FilePath = filePath;
        }
    }

    /// <summary>
    /// スキャン開始メッセージ
    /// </summary>
    public class ScanStartedMessage : ValueChangedMessage<bool>
    {
        public ScanStartedMessage(bool value) : base(value)
        {
        }
    }

    // ===== Generic bulk change messaging (Phase 1) =====
    public class ItemChangeData<T>
    {
        public enum ChangeType { Added, Updated, Deleted }

        public ChangeType Type { get; set; }
        public T Item { get; set; }

        public ItemChangeData(ChangeType type, T item)
        {
            Type = type;
            Item = item;
        }
    }

    public class BulkItemsChangedMessage<T> : ValueChangedMessage<List<ItemChangeData<T>>>
    {
        public BulkItemsChangedMessage(List<ItemChangeData<T>> value) : base(value)
        {
        }
    }
}
