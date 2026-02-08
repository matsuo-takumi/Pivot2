using CommunityToolkit.Mvvm.Messaging.Messages;
using Pivot.Engine.Models;

namespace Pivot.Messages
{
    public class MenuDisplayModeChangedMessage : ValueChangedMessage<MenuDisplayMode>
    {
        public MenuDisplayModeChangedMessage(MenuDisplayMode value) : base(value)
        {
        }
    }
}
