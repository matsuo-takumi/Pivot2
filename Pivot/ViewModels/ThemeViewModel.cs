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
        private readonly SettingsService _settingsService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ElementTheme _appTheme;

        [ObservableProperty]
        private BackdropType _appBackdropType;

        // (カスタムAcrylic/Luminosity 設定は削除)

        public ThemeViewModel(SettingsService settingsService, IMessenger messenger)
        {
            _settingsService = settingsService;
            _messenger = messenger;

            LoadCurrentSettings();
            IsActive = true; // メッセージの受信を開始
        }

        // Provide lists for binding (RadioButtons ItemsSource)
        public System.Collections.Generic.IEnumerable<BackdropType> BackdropTypes => System.Enum.GetValues(typeof(BackdropType)) as BackdropType[] ?? new BackdropType[0];

        public System.Collections.Generic.IEnumerable<ElementTheme> ElementThemes => System.Enum.GetValues(typeof(ElementTheme)) as ElementTheme[] ?? new ElementTheme[0];

        // Generated partial method hooks (MVVM Toolkit) to react to property changes
        partial void OnAppThemeChanged(ElementTheme value)
        {
            _ = _settingsService.SetTheme(value);
            _messenger.Send(new ThemeChangedMessage(value));
        }

        partial void OnAppBackdropTypeChanged(BackdropType value)
        {
            _ = _settingsService.SetBackdropType(value);
            _messenger.Send(new BackdropTypeChangedMessage(value));
        }

        private void LoadCurrentSettings()
        {
            // 初期ロード時はプロパティ経由で副作用を起こさないため、バックフィールドに直接設定する
            _appTheme = _settingsService.GetTheme();
            OnPropertyChanged(nameof(AppTheme));

            _appBackdropType = _settingsService.GetBackdropType();
            OnPropertyChanged(nameof(AppBackdropType));

            // (カスタム設定は削除)
        }

        [RelayCommand]
        private async Task SetAppTheme(ElementTheme theme)
        {
            if (AppTheme != theme)
            {
                AppTheme = theme;
                await _settingsService.SetTheme(theme);
                _messenger.Send(new ThemeChangedMessage(theme));
            }
        }

        [RelayCommand]
        private async Task SetAppBackdropType(BackdropType type)
        {
            if (AppBackdropType != type)
            {
                AppBackdropType = type;
                await _settingsService.SetBackdropType(type);
                _messenger.Send(new BackdropTypeChangedMessage(type));
            }
        }
    }
}
