using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Pivot.Converters;
using Pivot.Services;
using Pivot.ViewModels;
using System;

namespace Pivot.Views
{
    public sealed partial class ImageSettingsPage : Page
    {
        public ImageSettingsViewModel ViewModel { get; }

        public ImageSettingsPage()
        {
            // Get services
            var themeSettings = App.Current.Services.GetRequiredService<ThemeSettingsService>();
            ViewModel = new ImageSettingsViewModel(themeSettings);
            
            this.InitializeComponent();
            this.Loaded += ImageSettingsPage_Loaded;
        }

        private void ImageSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize ColorPicker with current color
            if (SelectionColorPicker != null)
            {
                var converter = new HexToColorConverter();
                if (converter.Convert(ViewModel.SelectionColor, typeof(Windows.UI.Color), null!, null!) is Windows.UI.Color color)
                {
                    SelectionColorPicker.Color = color;
                }
            }
        }

        private void SelectionColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        private void SelectionColorPicker_ColorChanged(Microsoft.UI.Xaml.Controls.ColorPicker sender, Microsoft.UI.Xaml.Controls.ColorChangedEventArgs args)
        {
            try
            {
                var converter = new HexToColorConverter();
                var hex = converter.ConvertBack(args.NewColor, typeof(string), string.Empty, string.Empty);
                if (hex is string hexString && !string.IsNullOrWhiteSpace(hexString))
                {
                    ViewModel.SelectionColor = hexString;
                }
            }
            catch (Exception)
            {
                // Ignore color conversion errors
            }
        }
    }
}
