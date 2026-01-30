using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.CodeModule.ViewModels;
using Windows.UI;

namespace Pivot.Views
{
    public sealed partial class CodeSettingsPage : Page
    {
        public CodeSettingsViewModel ViewModel { get; }

        public CodeSettingsPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<CodeSettingsViewModel>();
            
            // Initialize ColorPicker color
            if (!string.IsNullOrEmpty(ViewModel.CustomColorHex))
            {
                try
                {
                    var color = CodeModule.Services.CodeSettingsService.ParseHexColor(ViewModel.CustomColorHex);
                    ColorPickerControl.Color = color;
                }
                catch { }
            }
        }

        private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (ViewModel.IsCustomColor)
            {
                var color = args.NewColor;
                // Simple manual hex string creation
                var hex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                ViewModel.UpdateColorCommand.Execute(hex);
            }
        }
    }
}
