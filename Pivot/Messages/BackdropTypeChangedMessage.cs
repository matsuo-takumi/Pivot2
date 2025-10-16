using CommunityToolkit.Mvvm.Messaging.Messages;
using Pivot.Models; // BackdropType moved to Models

namespace Pivot.Messages
{
    public class BackdropTypeChangedMessage : ValueChangedMessage<BackdropType>
    {
        public BackdropTypeChangedMessage(BackdropType value) : base(value)
        {
        }
    }
}
