using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using System;
using System.Globalization;

namespace Pivot.Converters
{
    public class ElementThemeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ElementTheme elementTheme)
            {
                return elementTheme switch
                {
                    ElementTheme.Default => "システムの既定",
                    ElementTheme.Light => "ライト",
                    ElementTheme.Dark => "ダーク",
                    _ => value.ToString() ?? string.Empty
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // OneWay binding only - ConvertBack not used
            return value;
        }
    }
}
