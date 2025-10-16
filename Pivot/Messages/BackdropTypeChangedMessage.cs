using CommunityToolkit.Mvvm.Messaging.Messages;
using static Pivot.MainWindow;

namespace Pivot.Messages
{
    public class BackdropTypeChangedMessage : ValueChangedMessage<BackdropType>
    {
        public BackdropTypeChangedMessage(BackdropType value) : base(value)
        {
        }
    }
}
