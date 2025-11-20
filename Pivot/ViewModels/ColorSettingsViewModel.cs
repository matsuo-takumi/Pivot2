using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Messages;
using Pivot.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Windows.UI;

namespace Pivot.ViewModels
{
    public partial class ColorSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settings;

        public ColorSettingsViewModel(SettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ScratchpadColor = _settings.GetScratchpadEditorColor();
            UpdateBrushFromColor(ScratchpadColor);
            UpdateOverlayTextBrushFromHex(_settings.GetOverlayTintColor());
            WeakReferenceMessenger.Default.Register<ColorSettingsViewModel, OverlayColorChangedMessage>(this, (r, m) => r.UpdateOverlayTextBrushFromHex(m.Value));
        }

        [ObservableProperty]
        private string _scratchpadColor;

        [ObservableProperty]
        private SolidColorBrush _scratchpadPreview = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 255, 255));

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private SolidColorBrush _overlayTextBrush = new SolidColorBrush(Colors.Black);

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

        private void UpdateOverlayTextBrushFromHex(string? hex)
        {
            var color = ParseColorSafely(hex);
            var lightness = GetLightness(color);
            var brushColor = lightness >= 0.5 ? Colors.Black : Colors.White;
            OverlayTextBrush = new SolidColorBrush(brushColor);
        }

        private static Color ParseColorSafely(string? hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return Colors.Black;
                var value = hex.StartsWith("#") ? hex : "#" + hex;
                if (value.Length != 7) return Colors.Black;
                return ColorHelper.FromArgb(255,
                    Convert.ToByte(value.Substring(1, 2), 16),
                    Convert.ToByte(value.Substring(3, 2), 16),
                    Convert.ToByte(value.Substring(5, 2), 16));
            }
            catch
            {
                return Colors.Black;
            }
        }

        private static double GetLightness(Color color)
        {
            var r = color.R / 255.0;
            var g = color.G / 255.0;
            var b = color.B / 255.0;
            var max = Math.Max(Math.Max(r, g), b);
            var min = Math.Min(Math.Min(r, g), b);
            return (max + min) / 2.0;
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
    }
}


