using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using Windows.UI;

namespace Pivot.Converters
{
    public class SelectionToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSelected && isSelected)
            {
                // Get color and opacity from ThemeSettingsService
                try
                {
                    var themeSettings = App.Current.Services.GetService<ThemeSettingsService>();
                    if (themeSettings != null)
                    {
                        var hexColor = themeSettings.ImageSelectionColor;
                        var opacity = themeSettings.ImageSelectionOpacity;
                        
                        // Parse hex color
                        var color = ColorHelper.ParseColor(hexColor);
                        
                        // Apply opacity
                        return new SolidColorBrush(color) { Opacity = opacity };
                    }
                }
                catch
                {
                    // Fallback to default
                }
                
                // Fallback: DodgerBlue
                return new SolidColorBrush(Color.FromArgb(255, 30, 144, 255));
            }
            // Not selected: Transparent
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // OneWay binding only - ConvertBack not used
            return null!;
        }
    }
}
