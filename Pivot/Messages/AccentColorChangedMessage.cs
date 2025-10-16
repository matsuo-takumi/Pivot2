using CommunityToolkit.Mvvm.Messaging.Messages;
using Windows.UI;

namespace Pivot.Messages
{
    public class AccentColorChangedMessage : ValueChangedMessage<Color>
    {
        public AccentColorChangedMessage(Color value) : base(value)
        {
        }
    }
}
