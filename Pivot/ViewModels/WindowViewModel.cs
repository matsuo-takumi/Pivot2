using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Engine.Models;
using Pivot.Services;
using System.Threading.Tasks;

namespace Pivot.ViewModels
{
    public partial class WindowViewModel : ObservableRecipient
    {
        private readonly ThemeSettingsService _themeSettings;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private MenuDisplayMode _menuDisplayMode;

        public WindowViewModel(ThemeSettingsService themeSettings, IMessenger messenger)
        {
            _themeSettings = themeSettings;
            _messenger = messenger;

            LoadCurrentSettings();
            IsActive = true;
        }

        public System.Collections.Generic.IEnumerable<MenuDisplayMode> MenuDisplayModes => System.Enum.GetValues(typeof(MenuDisplayMode)) as MenuDisplayMode[] ?? new MenuDisplayMode[0];

        partial void OnMenuDisplayModeChanged(MenuDisplayMode value)
        {
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetMenuDisplayModeAsync(value),
                nameof(OnMenuDisplayModeChanged));
            _messenger.Send(new Pivot.Messages.MenuDisplayModeChangedMessage(value));
        }

        private void LoadCurrentSettings()
        {
            MenuDisplayMode = _themeSettings.MenuDisplayMode;
        }

        [RelayCommand]
        private async Task SetMenuDisplayMode(MenuDisplayMode mode)
        {
            if (MenuDisplayMode != mode)
            {
                MenuDisplayMode = mode;
                await _themeSettings.SetMenuDisplayModeAsync(mode);
            }
        }
    }
}
