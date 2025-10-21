using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Models;
using Pivot.Services;
using System.Threading.Tasks;

namespace Pivot.ViewModels
{
    public partial class WindowViewModel : ObservableRecipient
    {
        private readonly SettingsService _settingsService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private MenuDisplayMode _menuDisplayMode;

        public WindowViewModel(SettingsService settingsService, IMessenger messenger)
        {
            _settingsService = settingsService;
            _messenger = messenger;

            LoadCurrentSettings();
            IsActive = true;
        }

        // Provide list for binding (RadioButtons ItemsSource)
        public System.Collections.Generic.IEnumerable<MenuDisplayMode> MenuDisplayModes => System.Enum.GetValues(typeof(MenuDisplayMode)) as MenuDisplayMode[] ?? new MenuDisplayMode[0];

        // Generated partial method hooks (MVVM Toolkit) to react to property changes
        partial void OnMenuDisplayModeChanged(MenuDisplayMode value)
        {
            _ = _settingsService.SetMenuDisplayModeAsync(value);
            _messenger.Send(new Pivot.Messages.MenuDisplayModeChangedMessage(value));
        }

        private void LoadCurrentSettings()
        {
            MenuDisplayMode = _settingsService.GetMenuDisplayMode();
        }

        [RelayCommand]
        private async Task SetMenuDisplayMode(MenuDisplayMode mode)
        {
            if (MenuDisplayMode != mode)
            {
                MenuDisplayMode = mode;
                await _settingsService.SetMenuDisplayModeAsync(mode);
            }
        }
    }
}
