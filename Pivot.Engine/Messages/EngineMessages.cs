using CommunityToolkit.Mvvm.Messaging.Messages;
using Pivot.Engine.Models;
using System.Collections.Generic;

namespace Pivot.Engine.Messages
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

    public class DirectoryRemovedMessage : ValueChangedMessage<string>
    {
        public DirectoryRemovedMessage(string directoryPath) : base(directoryPath)
        {
        }
    }

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
