using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Pivot.Messages
{
    public class TagSelectionMessage : ValueChangedMessage<(string TabId, string TagName, bool IsSelected)>
    {
        public TagSelectionMessage(string tabId, string tagName, bool isSelected) : base((tabId, tagName, isSelected)) { }
    }
}


