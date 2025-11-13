using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Windows.UI;

namespace Pivot.ViewModels
{
    public partial class ColorSettingsViewModel : ObservableObject
    {
        private readonly SettingsService? _settings;

        public ColorSettingsViewModel(SettingsService? settings)
        {
            _settings = settings;
            ScratchpadColor = _settings?.GetScratchpadEditorColor() ?? "#FFFFFF";
            UpdateBrushFromColor(ScratchpadColor);
            // Initialize overlay color from settings (hex RRGGBB)
            try
            {
                var hex = _settings?.GetOverlayTintColor() ?? "#0000FF";
                if (!hex.StartsWith("#")) hex = "#" + hex;
                if (hex.Length == 7)
                {
                    OverlayColor = ColorHelper.FromArgb(255,
                        Convert.ToByte(hex.Substring(1, 2), 16),
                        Convert.ToByte(hex.Substring(3, 2), 16),
                        Convert.ToByte(hex.Substring(5, 2), 16));
                }
            }
            catch { }
            // Initialize overlay tint opacity from settings
            try
            {
                OverlayTintOpacity = _settings?.GetOverlayTintOpacity() ?? 0.5;
            }
            catch { OverlayTintOpacity = 0.5; }
        }

        [ObservableProperty]
        private string _scratchpadColor;

        [ObservableProperty]
        private SolidColorBrush _scratchpadPreview = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 255, 255));

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private Color _overlayColor = Color.FromArgb(255, 0, 0, 255);

        [ObservableProperty]
        private double _overlayTintOpacity = 0.5;

        partial void OnOverlayTintOpacityChanged(double value)
        {
            // Persist the new opacity and notify other parts of the app to refresh the overlay brush.
            _ = SaveOverlayTintOpacityAsync(value);
        }

        private async Task SaveOverlayTintOpacityAsync(double value)
        {
            try
            {
                if (_settings != null) await _settings.SetOverlayTintOpacityAsync(value);
                // send a message to refresh overlay (MainWindow listens to this message)
                try { WeakReferenceMessenger.Default.Send(new OverlayColorChangedMessage(string.Empty)); } catch { }
            }
            catch { }
        }

        partial void OnOverlayColorChanged(Color value)
        {
            // no-op for now; view binds directly
        }

        partial void OnScratchpadColorChanged(string value)
        {
            UpdateBrushFromColor(value);
        }

        private void UpdateBrushFromColor(string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var v = value.StartsWith("#") ? value : "#" + value;
                if (v.Length != 7) return;
                var c = ColorHelper.FromArgb(255,
                    Convert.ToByte(v.Substring(1, 2), 16),
                    Convert.ToByte(v.Substring(3, 2), 16),
                    Convert.ToByte(v.Substring(5, 2), 16));
                ScratchpadPreview = new SolidColorBrush(c);
            }
            catch { }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                var v = ScratchpadColor?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(v)) { StatusText = "Invalid"; return; }
                if (!v.StartsWith("#")) v = "#" + v;
                if (v.Length != 7) { StatusText = "Invalid"; return; }
                if (_settings != null) await _settings.SetScratchpadEditorColorAsync(v);
                StatusText = "Saved";
            }
            catch
            {
                StatusText = "Error";
            }
        }
        [RelayCommand]
        private async Task SaveOverlayAsync()
        {
            try
            {
                var c = OverlayColor;
                var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                if (_settings != null) await _settings.SetOverlayTintColorAsync(hex);
                // Notify other parts of app to update overlay brush
                try { WeakReferenceMessenger.Default.Send(new OverlayColorChangedMessage(hex)); } catch { }
                StatusText = "Saved";
            }
            catch
            {
                StatusText = "Error";
            }
        }
    }
}


