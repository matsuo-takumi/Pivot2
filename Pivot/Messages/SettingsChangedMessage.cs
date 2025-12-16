using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Pivot.Messages
{
    /// <summary>
    /// Message sent when a setting has changed.
    /// </summary>
    public class SettingsChangedMessage : ValueChangedMessage<string>
    {
        public SettingsChangedMessage(string settingName) : base(settingName)
        {
        }
    }
}
