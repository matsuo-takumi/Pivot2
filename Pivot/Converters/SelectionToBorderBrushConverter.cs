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
                // ThemeSettingsServiceから設定値を取得
                try
                {
                    var themeSettings = App.Current?.Services?.GetService(typeof(ThemeSettingsService)) as ThemeSettingsService;
                    if (themeSettings != null)
                    {
                        var colorHex = themeSettings.ImageSelectionColor;
                        var opacity = themeSettings.ImageSelectionOpacity;
                        
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
                return new SolidColorBrush(Color.FromArgb(200, 0, 120, 212));
            }
            // Not selected: 薄いグレーのボーダー（カードの境界が見えるように）
            return new SolidColorBrush(Color.FromArgb(32, 128, 128, 128)); // #20808080
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // OneWay binding only - ConvertBack not used
            return null!;
        }
    }
}
