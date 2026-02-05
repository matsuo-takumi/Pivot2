using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.CodeModule.Services;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;

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
        private string? _customColorHex = "#FF2D2D2D";

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


            TitleBrush = _settingsService.GetTitleBrush();
            TagTextBrush = _settingsService.GetTagTextBrush();
            TagBackgroundBrush = _settingsService.GetTagBackgroundBrush();
            TagBorderBrush = _settingsService.GetTagBorderBrush();
            LineNumberBrush = _settingsService.GetLineNumberBrush();

            // Efficiently update existing list items instead of re-populating
            if (Colors.Count == 0)
            {
                InitializeColors();
            }
            else
            {
                UpdateExistingColors();
            }
        }

        [ObservableProperty]
        private Brush? _editorTextBrush;

        [ObservableProperty]
        private Brush? _editorBackgroundBrush;

        // Old hex properties removed as they are now in the list items

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



        // New Visual Properties - List for UI
        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<CodeColorSettingViewModel> _colors = new();

        private void UpdateExistingColors()
        {
            foreach (var item in Colors)
            {
                string? newValue = null;
                switch (item.DisplayName)
                {
                    case "Editor Text": newValue = _settingsService.EditorTextColorHex; break;
                    case "Editor Background": newValue = _settingsService.EditorBackgroundColorHex; break;
                    case "Title": newValue = _settingsService.TitleColorHex; break;
                    case "Tag Text": newValue = _settingsService.TagTextColorHex; break;
                    case "Tag Background": newValue = _settingsService.TagBackgroundColorHex; break;
                    case "Tag Border": newValue = _settingsService.TagBorderColorHex; break;
                    case "Line Numbers": newValue = _settingsService.LineNumberColorHex; break;
                }

                if (item.HexValue != newValue)
                {
                    item.HexValue = newValue;
                }
            }
        }

        private void InitializeColors()
        {
            Colors.Clear();
            Colors.Add(new CodeColorSettingViewModel("Editor Text", "Main text color for code.", _settingsService.EditorTextColorHex, "SystemControlPageTextBaseHighBrush", async (hex) => await _settingsService.SetEditorTextColorAsync(hex)));
            Colors.Add(new CodeColorSettingViewModel("Editor Background", "Background color of the editor.", _settingsService.EditorBackgroundColorHex, "SystemControlPageBackgroundChromeLowBrush", async (hex) => await _settingsService.SetEditorBackgroundColorAsync(hex)));
            Colors.Add(new CodeColorSettingViewModel("Line Numbers", "Color of the line numbers.", _settingsService.LineNumberColorHex, "SystemControlForegroundBaseMediumBrush", async (hex) => await _settingsService.SetLineNumberColorAsync(hex)));
            Colors.Add(new CodeColorSettingViewModel("Title", "Title text color.", _settingsService.TitleColorHex, "SystemControlPageTextBaseHighBrush", async (hex) => await _settingsService.SetTitleColorAsync(hex)));
            Colors.Add(new CodeColorSettingViewModel("Tag Text", "Text color for tags.", _settingsService.TagTextColorHex, "SystemControlPageTextBaseHighBrush", async (hex) => await _settingsService.SetTagTextColorAsync(hex)));
            Colors.Add(new CodeColorSettingViewModel("Tag Background", "Background color for tags.", _settingsService.TagBackgroundColorHex, "SubtleFillColorSecondaryBrush", async (hex) => await _settingsService.SetTagBackgroundColorAsync(hex)));
            Colors.Add(new CodeColorSettingViewModel("Tag Border", "Border color for tags.", _settingsService.TagBorderColorHex, "CardStrokeColorDefaultBrush", async (hex) => await _settingsService.SetTagBorderColorAsync(hex)));
        }

        // Keep Brushes for binding in other ViewModels (Editor/Cards), but UI binds to 'Colors' list.
        [ObservableProperty] private Brush? _titleBrush;
        [ObservableProperty] private Brush? _tagTextBrush;
        [ObservableProperty] private Brush? _tagBackgroundBrush;
        [ObservableProperty] private Brush? _tagBorderBrush;
        [ObservableProperty] private Brush? _lineNumberBrush;
        
    }
}
