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

        public CodeSettingsViewModel(CodeSettingsService settingsService)
        {
            _settingsService = settingsService;
            
            _settingsService.SettingsChanged += (s, e) => Refresh();
            Refresh();
        }

        private void Refresh()
        {
            IsCustomColor = _settingsService.CurrentMode == CodeSettingsService.BackgroundMode.Custom;
            CustomColorHex = _settingsService.CustomColorHex;
            
            var brush = _settingsService.GetBackgroundBrush();
            CardBackground = brush;

            EditorTextBrush = _settingsService.GetEditorTextBrush();
            EditorBackgroundBrush = _settingsService.GetEditorBackgroundBrush();
            // Also expose hex strings for two-way binding with ColorPicker if needed, 
            // but ColorPicker usually binds to Color. We might need a converter or property.
            EditorTextColorHex = _settingsService.EditorTextColorHex;
            EditorBackgroundColorHex = _settingsService.EditorBackgroundColorHex;
        }

        [ObservableProperty]
        private Brush? _editorTextBrush;

        [ObservableProperty]
        private Brush? _editorBackgroundBrush;

        [ObservableProperty]
        private string _editorTextColorHex = "#FFFFFFFF";

        [ObservableProperty]
        private string _editorBackgroundColorHex = "#FF1E1E1E";

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

        [RelayCommand]
        private async Task UpdateEditorTextColorAsync(string hex)
        {
             if (string.IsNullOrEmpty(hex)) return;
            try 
            {
                var color = CodeSettingsService.ParseHexColor(hex);
                await _settingsService.SetEditorTextColorAsync(hex);
            }
            catch {}
        }

        [RelayCommand]
        private async Task UpdateEditorBackgroundColorAsync(string hex)
        {
             if (string.IsNullOrEmpty(hex)) return;
            try 
            {
                var color = CodeSettingsService.ParseHexColor(hex);
                await _settingsService.SetEditorBackgroundColorAsync(hex);
            }
            catch {}
        }
    }
}
