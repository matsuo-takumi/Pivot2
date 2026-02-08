using CommunityToolkit.Mvvm.Messaging.Messages;
using Pivot.Engine.Models;

namespace Pivot.Messages
{
    public class NavigationRequestMessage : ValueChangedMessage<NavigationRegion>
    {
        public NavigationRequestMessage(NavigationRegion value) : base(value) { }
    }
}


