using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Threading.Tasks;

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
        }

        [ObservableProperty]
        private string _scratchpadColor;

        [ObservableProperty]
        private SolidColorBrush _scratchpadPreview = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 255, 255));

        [ObservableProperty]
        private string _statusText = string.Empty;

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
    }
}


