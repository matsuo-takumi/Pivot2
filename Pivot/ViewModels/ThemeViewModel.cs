using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Pivot.Messages;
using Pivot.Engine.Models;
using Pivot.Services;
using System;
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

        [ObservableProperty]
        private int _lightStartHour;

        [ObservableProperty]
        private int _darkStartHour;

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
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeViewModel] OnAppThemeChanged: {value}");
                Utilities.SafeAsync.FireAndForget(
                    _themeSettings.SetThemeAsync(value),
                    nameof(OnAppThemeChanged));
                // Send EffectiveTheme (resolved to Light/Dark) so UI applies correctly
                var effectiveTheme = _themeSettings.EffectiveTheme;
                System.Diagnostics.Debug.WriteLine($"[ThemeViewModel] Sending ThemeChangedMessage: {effectiveTheme}");
                _messenger.Send(new ThemeChangedMessage(effectiveTheme));
                System.Diagnostics.Debug.WriteLine($"[ThemeViewModel] ThemeChangedMessage sent successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeViewModel] OnAppThemeChanged ERROR: {ex}");
            }
        }

        partial void OnAppBackdropTypeChanged(BackdropType value)
        {
            if (_isLoadingThemeSettings) return;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeViewModel] OnAppBackdropTypeChanged: {value}");
                Utilities.SafeAsync.FireAndForget(
                    _themeSettings.SetBackdropTypeAsync(value),
                    nameof(OnAppBackdropTypeChanged));
                _messenger.Send(new BackdropTypeChangedMessage(value));
                System.Diagnostics.Debug.WriteLine($"[ThemeViewModel] BackdropTypeChangedMessage sent successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeViewModel] OnAppBackdropTypeChanged ERROR: {ex}");
            }
        }

        partial void OnLightStartHourChanged(int value)
        {
            if (_isLoadingThemeSettings) return;
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetLightStartHourAsync(value),
                nameof(OnLightStartHourChanged));
        }

        partial void OnDarkStartHourChanged(int value)
        {
            if (_isLoadingThemeSettings) return;
            Utilities.SafeAsync.FireAndForget(
                _themeSettings.SetDarkStartHourAsync(value),
                nameof(OnDarkStartHourChanged));
        }

        private void LoadCurrentSettings()
        {
            AppTheme = _themeSettings.AppTheme;
            AppBackdropType = _themeSettings.AppBackdropType;
            LightStartHour = _themeSettings.LightStartHour;
            DarkStartHour = _themeSettings.DarkStartHour;
        }

        [RelayCommand]
        private void SetAppTheme(ElementTheme theme)
        {
            // Just set the property - OnAppThemeChanged handles the rest
            // This avoids duplicate message sending and race conditions
            if (AppTheme != theme)
            {
                AppTheme = theme;
            }
        }

        [RelayCommand]
        private void SetAppBackdropType(BackdropType type)
        {
            // Just set the property - OnAppBackdropTypeChanged handles the rest
            if (AppBackdropType != type)
            {
                AppBackdropType = type;
            }
        }
    }
}
