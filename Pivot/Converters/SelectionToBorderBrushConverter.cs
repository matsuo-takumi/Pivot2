using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
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
                try
                {
                    var settings = App.Current?.Services?.GetService(typeof(SettingsService)) as SettingsService;
                    if (settings != null)
                    {
                        var colorHex = settings.GetImageSelectionColor();
                        var opacity = settings.GetImageSelectionOpacity();
                        
                        // Parse hex color
                        var trimmed = colorHex.StartsWith("#") ? colorHex : "#" + colorHex;
                        if (trimmed.Length == 7)
                        {
                            var r = System.Convert.ToByte(trimmed.Substring(1, 2), 16);
                            var g = System.Convert.ToByte(trimmed.Substring(3, 2), 16);
                            var b = System.Convert.ToByte(trimmed.Substring(5, 2), 16);
                            var alpha = (byte)(opacity * 255);
                            return new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
                        }
                    }
                }
                catch { }
                
                // Fallback: Default semi-transparent blue
                return new SolidColorBrush(Color.FromArgb(128, 0, 120, 212));
            }
            // Not selected: Transparent
            return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

