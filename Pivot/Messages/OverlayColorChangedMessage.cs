using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Pivot.Messages
{
    public class OverlayColorChangedMessage : ValueChangedMessage<string>
    {
        public OverlayColorChangedMessage(string value) : base(value) { }
    }
}


