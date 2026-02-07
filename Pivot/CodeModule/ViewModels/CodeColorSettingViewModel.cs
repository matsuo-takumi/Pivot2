using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeColorSettingViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustom))]
        private string? _hexValue;

        [ObservableProperty]
        private Brush _previewBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        public bool IsCustom => !string.IsNullOrEmpty(HexValue);

        private readonly string? _defaultResourceKey;
        private readonly Func<string?, Task> _updateAction;

        public CodeColorSettingViewModel(string displayName, string description, string? initialHex, string? defaultResourceKey, Func<string?, Task> updateAction)
        {
            _displayName = displayName;
            _description = description;
            _hexValue = initialHex;
            _defaultResourceKey = defaultResourceKey;
            _updateAction = updateAction;
            UpdateBrush(initialHex);
        }

        partial void OnHexValueChanged(string? value)
        {
            UpdateBrush(value);
            // Throttle or debounce could remain in the main VM or Service, 
            // here we just trigger the update action directly or on lost focus (via command)
            // For simplicity, we trigger update immediately if valid hex, 
            // but let's assume the user commits via enter or lost focus binding in XAML, 
            // OR we just observe changes.
            // Actually, mirroring current logic: Update when property changes.
            _updateAction?.Invoke(value);
        }

        [RelayCommand]
        private void ResetToDefault()
        {
            HexValue = null;
        }

        public void UpdateColorFromPicker(Windows.UI.Color color)
        {
            var hex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            HexValue = hex;
        }

        private void UpdateBrush(string? hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                // Try to resolve default from resource
                if (!string.IsNullOrEmpty(_defaultResourceKey) && 
                    Application.Current.Resources.TryGetValue(_defaultResourceKey, out var res) && 
                    res is SolidColorBrush themeBrush)
                {
                    PreviewBrush = themeBrush;
                }
                else
                {
                    // Fallback if no resource key or lookup failed
                    PreviewBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            }
            else
            {
                try
                {
                    var color = Pivot.CodeModule.Services.CodeSettingsService.ParseHexColor(hex);
                    PreviewBrush = new SolidColorBrush(color);
                }
                catch 
                {
                    // Keep preview valid or transparent on error
                }
            }
        }
    }
}
