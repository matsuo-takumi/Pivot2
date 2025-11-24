using Microsoft.UI.Xaml.Data;
using Microsoft.UI;
using System;
using Windows.UI;

namespace Pivot.Converters
{
    public class HexToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string hex)
            {
                try
                {
                    var trimmed = hex.StartsWith("#") ? hex : "#" + hex;
                    if (trimmed.Length == 7)
                    {
                        return ColorHelper.FromArgb(255,
                            System.Convert.ToByte(trimmed.Substring(1, 2), 16),
                            System.Convert.ToByte(trimmed.Substring(3, 2), 16),
                            System.Convert.ToByte(trimmed.Substring(5, 2), 16));
                    }
                }
                catch { }
            }
            return ColorHelper.FromArgb(255, 0, 120, 212); // Default blue
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "#0078D4"; // Default blue
        }
    }
}

