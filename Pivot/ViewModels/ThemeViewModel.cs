using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Pivot.Messages;
using Pivot.Models;
using Pivot.Services;
using System.Threading.Tasks;

namespace Pivot.ViewModels
{
    public partial class ThemeViewModel : ObservableRecipient
    {
        private readonly ThemeSettingsService _themeSettings;
        private readonly IMessenger _messenger;
        private bool _isLoadingThemeSettings;

        [ObservableProperty]
        private ElementTheme _appTheme;

        [ObservableProperty]
        private BackdropType _appBackdropType;

        public OverlayTintViewModel OverlayTint { get; }

        public ThemeViewModel(ThemeSettingsService themeSettings, IMessenger messenger)
        {
            _themeSettings = themeSettings;
            _messenger = messenger;
            OverlayTint = new OverlayTintViewModel(_themeSettings, _messenger);

            _isLoadingThemeSettings = true;
            LoadCurrentSettings();
            _isLoadingThemeSettings = false;
            IsActive = true;
        }

        public System.Collections.Generic.IEnumerable<BackdropType> BackdropTypes => System.Enum.GetValues(typeof(BackdropType)) as BackdropType[] ?? new BackdropType[0];

        public System.Collections.Generic.IEnumerable<ElementTheme> ElementThemes => System.Enum.GetValues(typeof(ElementTheme)) as ElementTheme[] ?? new ElementTheme[0];

        partial void OnAppThemeChanged(ElementTheme value)
        {
            if (_isLoadingThemeSettings) return;
            _ = _themeSettings.SetThemeAsync(value);
            _messenger.Send(new ThemeChangedMessage(value));
        }

        partial void OnAppBackdropTypeChanged(BackdropType value)
        {
            if (_isLoadingThemeSettings) return;
            _ = _themeSettings.SetBackdropTypeAsync(value);
            _messenger.Send(new BackdropTypeChangedMessage(value));
        }

        private void LoadCurrentSettings()
        {
            AppTheme = _themeSettings.AppTheme;
            AppBackdropType = _themeSettings.AppBackdropType;
        }

        [RelayCommand]
        private async Task SetAppTheme(ElementTheme theme)
        {
            if (AppTheme != theme)
            {
                AppTheme = theme;
                await _themeSettings.SetThemeAsync(theme);
                _messenger.Send(new ThemeChangedMessage(theme));
            }
        }

        [RelayCommand]
        private async Task SetAppBackdropType(BackdropType type)
        {
            if (AppBackdropType != type)
            {
                AppBackdropType = type;
                await _themeSettings.SetBackdropTypeAsync(type);
                _messenger.Send(new BackdropTypeChangedMessage(type));
            }
        }
    }
}
