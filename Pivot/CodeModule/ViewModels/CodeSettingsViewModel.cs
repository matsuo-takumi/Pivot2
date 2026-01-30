using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.CodeModule.Services;
using System.Threading.Tasks;
using Microsoft.UI;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeSettingsViewModel : ObservableObject
    {
        private readonly CodeSettingsService _settingsService;
        
        [ObservableProperty]
        private Brush? _cardBackground;

        [ObservableProperty]
        private bool _isCustomColor;

        [ObservableProperty]
        private string _customColorHex = "#FF2D2D2D";
        
        public static CodeSettingsViewModel? Instance { get; private set; }

        public CodeSettingsViewModel(CodeSettingsService settingsService)
        {
            _settingsService = settingsService;
            Instance = this;
            
            _settingsService.SettingsChanged += (s, e) => Refresh();
            Refresh();
        }

        private void Refresh()
        {
            IsCustomColor = _settingsService.CurrentMode == CodeSettingsService.BackgroundMode.Custom;
            CustomColorHex = _settingsService.CustomColorHex;
            
            var brush = _settingsService.GetBackgroundBrush();
            CardBackground = brush;
        }

        [RelayCommand]
        private async Task SetThemeModeAsync()
        {
            await _settingsService.SetModeAsync(CodeSettingsService.BackgroundMode.Theme);
        }

        [RelayCommand]
        private async Task SetCustomColorModeAsync()
        {
            await _settingsService.SetModeAsync(CodeSettingsService.BackgroundMode.Custom);
        }

        [RelayCommand]
        private async Task UpdateColorAsync(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return;
            // Validate via parser
            try 
            {
                var color = CodeSettingsService.ParseHexColor(hex);
                await _settingsService.SetCustomColorAsync(hex);
            }
            catch {}
        }
    }
}
